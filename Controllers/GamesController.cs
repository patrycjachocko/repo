// Plik: /Controllers/GamesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using praca_dyplomowa_zesp.Models.API; // Używamy tych samych modeli API co w LibraryController
using Microsoft.AspNetCore.Identity;
using praca_dyplomowa_zesp.Models.Users;
using Microsoft.AspNetCore.Authorization;

// Definiujemy ViewModele dla tego kontrolera
namespace praca_dyplomowa_zesp.Models.Modules.Games
{
    public class GameBrowserViewModel
    {
        public List<ApiGame> Games { get; set; } = new List<ApiGame>();
        public int CurrentPage { get; set; } = 1;
        public string? SearchString { get; set; }

        // NOWE: Przechowuje tryb, w jakim jest przeglądarka ("browse" lub "guides")
        public string Mode { get; set; } = "browse";
    }

    // NOWE: Dodajemy z powrotem GameDetailViewModel dla widoku szczegółów gier spoza biblioteki
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
        // ZMIANA: Dodano parametr "mode"
        public async Task<IActionResult> Index(string? searchString, int page = 1, string mode = "browse")
        {
            if (page < 1) page = 1;
            int offset = (page - 1) * PageSize;
            string query;

            if (!string.IsNullOrEmpty(searchString) && mode == "browse")
            {
                // Wyszukiwanie gier
                query = $"fields name, cover.url; search \"{searchString}\"; where cover.url != null & parent_game = null; limit {PageSize}; offset {offset};";
            }
            else
            {
                // Domyślny widok - popularne gry (dla "guides" i domyślnego "browse")
                query = $"fields name, cover.url, rating; sort rating desc; where rating != null & cover.url != null & parent_game = null; limit {PageSize}; offset {offset};";
            }

            var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);

            var gamesFromApi = string.IsNullOrEmpty(jsonResponse)
                ? new List<ApiGame>()
                : JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse) ?? new List<ApiGame>();

            // ZMIANA: Ustawienie tytułu na podstawie trybu
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
                Games = gamesFromApi.Select(apiGame => new ApiGame
                {
                    Id = apiGame.Id,
                    Name = apiGame.Name,
                    Cover = apiGame.Cover != null ? new ApiCover { Url = apiGame.Cover.Url.Replace("t_thumb", "t_cover_big") } : null
                }).ToList(),
                CurrentPage = page,
                SearchString = searchString,
                Mode = mode // ZMIANA: Przekazanie trybu do widoku
            };

            return View(viewModel);
        }

        // GET: Games/Details/5 (gdzie 5 to ID gry z IGDB)
        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            var currentUserId = GetCurrentUserId();

            // Krok 1: Sprawdź, czy użytkownik jest zalogowany i ma tę grę w bibliotece
            if (currentUserId != Guid.Empty)
            {
                var gameInLibrary = await _context.GamesInLibraries
                    .FirstOrDefaultAsync(g => g.UserId == currentUserId && g.IgdbGameId == id);

                if (gameInLibrary != null)
                {
                    // Użytkownik ma grę - przekieruj do widoku szczegółów w bibliotece
                    return RedirectToAction("Details", "Library", new { id = gameInLibrary.Id });
                }
            }

            // Krok 2: Użytkownik nie ma gry lub nie jest zalogowany. Pokaż widok publiczny.
            // Używamy zapytania podobnego do tego z LibraryController.Details
            var gameQuery = $"fields name, cover.url, genres.name, involved_companies.company.name, involved_companies.developer, release_dates.human; where id = {id}; limit 1;";
            var gameJsonResponse = await _igdbClient.ApiRequestAsync("games", gameQuery);

            if (string.IsNullOrEmpty(gameJsonResponse)) return NotFound();

            var gameDetailsFromApi = (JsonConvert.DeserializeObject<List<ApiGame>>(gameJsonResponse) ?? new List<ApiGame>()).FirstOrDefault();

            if (gameDetailsFromApi == null) return NotFound();

            // Krok 3: Stwórz ViewModel
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

        // --- NOWA METODA: Przekierowanie ze Steam ---
        [HttpGet]
        public async Task<IActionResult> SteamRedirect(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return RedirectToAction("Index");
            }

            // Zabezpieczenie przed cudzysłowami w nazwie gry, które mogłyby zepsuć zapytanie IGDB
            var safeQuery = query.Replace("\"", "");

            // Szukamy gry w IGDB po nazwie otrzymanej ze Steam
            // fields id, name; -> potrzebujemy tylko ID, by zrobić przekierowanie
            var igdbQuery = $"fields id, name; search \"{safeQuery}\"; limit 1;";

            try
            {
                var jsonResponse = await _igdbClient.ApiRequestAsync("games", igdbQuery);

                var games = string.IsNullOrEmpty(jsonResponse)
                    ? new List<ApiGame>()
                    : JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse) ?? new List<ApiGame>();

                var foundGame = games.FirstOrDefault();

                if (foundGame != null)
                {
                    // Sukces! Mamy ID gry w naszym systemie (IGDB), przekierowujemy do Details
                    return RedirectToAction("Details", new { id = foundGame.Id });
                }
            }
            catch (Exception ex)
            {
                // Logowanie błędu można dodać tutaj
                Console.WriteLine($"Błąd podczas wyszukiwania gry ze Steam: {ex.Message}");
            }

            // Fallback: Jeśli nie znaleziono gry automatycznie, przekieruj do wyszukiwarki z wpisaną nazwą
            TempData["ErrorMessage"] = $"Nie udało się automatycznie dopasować gry '{query}'. Spróbuj znaleźć ją na liście poniżej.";
            return RedirectToAction("Index", new { searchString = query });
        }
    }
}