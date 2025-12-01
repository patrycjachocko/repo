using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using praca_dyplomowa_zesp.Models.API;
using Microsoft.AspNetCore.Identity;
using praca_dyplomowa_zesp.Models.Users;
using Microsoft.AspNetCore.Authorization;

namespace praca_dyplomowa_zesp.Models.Modules.Games
{
    public class GameBrowserViewModel
    {
        public List<ApiGame> Games { get; set; } = new List<ApiGame>();
        public int CurrentPage { get; set; } = 1;
        public string? SearchString { get; set; }
        public string Mode { get; set; } = "browse";
    }

    public class GameDetailViewModel
    {
        public long IgdbGameId { get; set; }
        public string Name { get; set; }
        public string? CoverUrl { get; set; }
        public List<string> Genres { get; set; } = new List<string>();
        public string? Developer { get; set; }
        public string? ReleaseDate { get; set; }
    }
}

namespace praca_dyplomowa_zesp.Controllers
{
    [AllowAnonymous]
    public class GamesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IGDBClient _igdbClient;
        private readonly UserManager<User> _userManager;

        private const int PageSize = 50;

        public GamesController(ApplicationDbContext context, IGDBClient igdbClient, UserManager<User> userManager)
        {
            _context = context;
            _igdbClient = igdbClient;
            _userManager = userManager;
        }

        private Guid GetCurrentUserId()
        {
            var userIdString = _userManager.GetUserId(User);
            if (Guid.TryParse(userIdString, out Guid userId))
            {
                return userId;
            }
            return Guid.Empty;
        }

        // GET: Games
        public async Task<IActionResult> Index(string? searchString, int page = 1, string mode = "browse")
        {
            if (page < 1) page = 1;
            int offset = (page - 1) * PageSize;

            string safeSearch = searchString?.Replace("\"", "").Trim();
            string query;

            // KROK 1: Pobieranie danych z API
            // Prosimy o category i version_parent
            string fields = "fields name, cover.url, rating, category, version_parent";
            // parent_game = null (odrzuca DLC)
            string baseFilter = "where parent_game = null";

            if (!string.IsNullOrEmpty(safeSearch) && mode == "browse")
            {
                // Wyszukiwanie: Limit zwiększony do 100, aby mieć zapas po odfiltrowaniu
                query = $"{fields}; search \"{safeSearch}\"; {baseFilter} & cover.url != null; limit 100; offset {offset};";
            }
            else
            {
                // Domyślny widok
                query = $"{fields}; sort rating desc; {baseFilter} & rating != null & cover.url != null; limit 100; offset {offset};";
            }

            var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);

            var gamesFromApi = string.IsNullOrEmpty(jsonResponse)
                ? new List<ApiGame>()
                : JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse) ?? new List<ApiGame>();

            // KROK 2: Agresywne filtrowanie w C#
            // Odrzucamy wszystko co wygląda na pakiet, kolekcję lub inną wersję
            var filteredGames = gamesFromApi
                .Where(g =>
                    // Musi być główną grą (Category 0)
                    g.Category == 0 &&
                    // Nie może być Legacy Edition itp.
                    g.Version_parent == null &&
                    // Dodatkowe filtrowanie po nazwie (eliminuje Bundle, Kolekcje, Pakiety)
                    !string.IsNullOrEmpty(g.Name) &&
                    !g.Name.Contains("Bundle", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.Contains("Collection", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.Contains("Anthology", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.EndsWith("Pack", StringComparison.OrdinalIgnoreCase) // Np. "Expansion Pack"
                )
                .Take(PageSize)
                .ToList();

            if (mode == "guides")
            {
                ViewData["Title"] = "Guides & Tips - Wybierz grę";
            }
            else
            {
                ViewData["Title"] = "Przeglądaj Gry";
            }

            var viewModel = new Models.Modules.Games.GameBrowserViewModel
            {
                Games = filteredGames.Select(apiGame => new ApiGame
                {
                    Id = apiGame.Id,
                    Name = apiGame.Name,
                    Cover = apiGame.Cover != null ? new ApiCover { Url = apiGame.Cover.Url.Replace("t_thumb", "t_cover_big") } : null
                }).ToList(),
                CurrentPage = page,
                SearchString = searchString,
                Mode = mode
            };

            return View(viewModel);
        }

        // GET: Games/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            var currentUserId = GetCurrentUserId();

            if (currentUserId != Guid.Empty)
            {
                var gameInLibrary = await _context.GamesInLibraries
                    .FirstOrDefaultAsync(g => g.UserId == currentUserId && g.IgdbGameId == id);

                if (gameInLibrary != null)
                {
                    return RedirectToAction("Details", "Library", new { id = gameInLibrary.Id });
                }
            }

            var gameQuery = $"fields name, cover.url, genres.name, involved_companies.company.name, involved_companies.developer, release_dates.human; where id = {id}; limit 1;";
            var gameJsonResponse = await _igdbClient.ApiRequestAsync("games", gameQuery);

            if (string.IsNullOrEmpty(gameJsonResponse)) return NotFound();

            var gameDetailsFromApi = (JsonConvert.DeserializeObject<List<ApiGame>>(gameJsonResponse) ?? new List<ApiGame>()).FirstOrDefault();

            if (gameDetailsFromApi == null) return NotFound();

            var viewModel = new Models.Modules.Games.GameDetailViewModel
            {
                IgdbGameId = gameDetailsFromApi.Id,
                Name = gameDetailsFromApi.Name,
                CoverUrl = gameDetailsFromApi.Cover?.Url?.Replace("t_thumb", "t_cover_big"),
                Genres = gameDetailsFromApi.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                Developer = gameDetailsFromApi.Involved_companies?.FirstOrDefault(ic => ic.developer)?.Company?.Name,
                ReleaseDate = gameDetailsFromApi.Release_dates?.FirstOrDefault()?.Human
            };

            return View(viewModel);
        }

        // Redirect ze Steam
        [HttpGet]
        public async Task<IActionResult> SteamRedirect(string query)
        {
            if (string.IsNullOrEmpty(query)) return RedirectToAction("Index");

            var safeQuery = query.Replace("\"", "").Trim();

            // Pobieramy więcej wyników, żeby móc filtrować
            var igdbQuery = $"fields id, name, category, version_parent; search \"{safeQuery}\"; where parent_game = null; limit 20;";

            try
            {
                var jsonResponse = await _igdbClient.ApiRequestAsync("games", igdbQuery);

                var games = string.IsNullOrEmpty(jsonResponse)
                    ? new List<ApiGame>()
                    : JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse) ?? new List<ApiGame>();

                // Stosujemy to samo filtrowanie co w Index
                var foundGame = games.FirstOrDefault(g =>
                    g.Category == 0 &&
                    g.Version_parent == null &&
                    !string.IsNullOrEmpty(g.Name) &&
                    !g.Name.Contains("Bundle", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.Contains("Collection", StringComparison.OrdinalIgnoreCase)
                );

                if (foundGame != null)
                {
                    return RedirectToAction("Details", new { id = foundGame.Id });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas wyszukiwania gry ze Steam: {ex.Message}");
            }

            TempData["ErrorMessage"] = $"Nie udało się automatycznie dopasować gry '{query}'. Spróbuj znaleźć ją na liście poniżej.";
            return RedirectToAction("Index", new { searchString = query });
        }
    }
}