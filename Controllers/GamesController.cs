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
// Można je też przenieść do osobnych plików w Models/Modules/Games/
namespace praca_dyplomowa_zesp.Models.Modules.Games
{
    public class GameBrowserViewModel
    {
        public List<ApiGame> Games { get; set; } = new List<ApiGame>();
        public int CurrentPage { get; set; } = 1;
        public string? SearchString { get; set; }
    }

    public class GameDetailViewModel
    {
        public long IgdbGameId { get; set; }
        public string Name { get; set; } = "Brak nazwy";
        public string? CoverUrl { get; set; }
        public List<string> Genres { get; set; } = new List<string>();
        public string? Developer { get; set; }
        public string? ReleaseDate { get; set; }
        public bool IsInLibrary { get; set; } = false; // Czy gra jest już w bibliotece usera?
    }
}


namespace praca_dyplomowa_zesp.Controllers
{
    // Ten kontroler jest dostępny dla wszystkich (również niezalogowanych)
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

        /// <summary>
        /// Metoda pomocnicza do pobierania ID (Guid) aktualnie zalogowanego użytkownika
        /// </summary>
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
        // Obsługuje wyszukiwanie i paginację
        public async Task<IActionResult> Index(string? searchString, int page = 1)
        {
            if (page < 1) page = 1;
            int offset = (page - 1) * PageSize;
            string query;

            if (!string.IsNullOrEmpty(searchString))
            {
                // Wyszukiwanie gier
                // --- ZMIANA: Dodano 'where cover.url != null & parent_game = null' ---
                query = $"fields name, cover.url; search \"{searchString}\"; where cover.url != null & parent_game = null; limit {PageSize}; offset {offset};";
            }
            else
            {
                // Domyślny widok - popularne gry
                // --- ZMIANA: Dodano '& parent_game = null' ---
                query = $"fields name, cover.url, rating; sort rating desc; where rating != null & cover.url != null & parent_game = null; limit {PageSize}; offset {offset};";
            }

            var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);

            var gamesFromApi = string.IsNullOrEmpty(jsonResponse)
                ? new List<ApiGame>()
                : JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse) ?? new List<ApiGame>();

            var viewModel = new Models.Modules.Games.GameBrowserViewModel
            {
                Games = gamesFromApi.Select(apiGame => new ApiGame
                {
                    Id = apiGame.Id,
                    Name = apiGame.Name,
                    Cover = apiGame.Cover != null ? new ApiCover { Url = apiGame.Cover.Url.Replace("t_thumb", "t_cover_big") } : null
                }).ToList(),
                CurrentPage = page,
                SearchString = searchString
            };

            return View(viewModel);
        }

        // GET: Games/Details/5 (gdzie 5 to IGDB Game ID)
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null) return NotFound();

            long igdbGameId = id.Value;

            // Zapytanie o szczegóły gry
            var gameQuery = $"fields name, cover.url, genres.name, involved_companies.company.name, involved_companies.developer, release_dates.human; where id = {igdbGameId}; limit 1;";
            var gameJsonResponse = await _igdbClient.ApiRequestAsync("games", gameQuery);
            var gameDetailsFromApi = (JsonConvert.DeserializeObject<List<ApiGame>>(gameJsonResponse) ?? new List<ApiGame>()).FirstOrDefault();

            if (gameDetailsFromApi == null) return NotFound();

            bool isInLibrary = false;
            // Sprawdzamy, czy gra jest w bibliotece (tylko jeśli user jest zalogowany)
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId != Guid.Empty)
                {
                    isInLibrary = await _context.GamesInLibraries
                        .AnyAsync(g => g.UserId == currentUserId && g.IgdbGameId == igdbGameId);
                }
            }

            var viewModel = new Models.Modules.Games.GameDetailViewModel
            {
                IgdbGameId = gameDetailsFromApi.Id,
                Name = gameDetailsFromApi.Name ?? "Brak nazwy",
                CoverUrl = gameDetailsFromApi.Cover?.Url?.Replace("t_thumb", "t_cover_big"),
                Genres = gameDetailsFromApi.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                Developer = gameDetailsFromApi.Involved_companies?.FirstOrDefault(ic => ic.developer)?.Company?.Name,
                ReleaseDate = gameDetailsFromApi.Release_dates?.FirstOrDefault()?.Human,
                IsInLibrary = isInLibrary
            };

            return View(viewModel);
        }
    }
}