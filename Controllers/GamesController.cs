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
        public bool ShowIgdbUser { get; set; } = true;   // Gracze IGDB
        public bool ShowIgdbCritic { get; set; } = true; // Krytycy IGDB
        public bool ShowLocal { get; set; } = true;      // Twoja społeczność
    }

    public class GameDetailViewModel
    {
        public long IgdbGameId { get; set; }
        public string Name { get; set; }
        public string? CoverUrl { get; set; }
        public List<string> Genres { get; set; } = new List<string>();
        public string? Developer { get; set; }
        public string? ReleaseDate { get; set; }
        public ViewModels.GameRatingViewModel Ratings { get; set; }
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

        private const int PageSize = 60;

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
        public async Task<IActionResult> Index(
            string? searchString,
            int page = 1,
            string mode = "browse",
            bool isFiltered = false,
            bool showUser = false,
            bool showCritic = false,
            bool showLocal = false
        )
        {
            // LOGIKA DOMYŚLNYCH WARTOŚCI (Pierwsze wejście = wszystko włączone)
            if (!isFiltered)
            {
                showUser = true;
                showCritic = true;
                showLocal = true;
            }

            if (page < 1) page = 1;
            int offset = (page - 1) * PageSize;

            string safeSearch = searchString?.Replace("\"", "").Trim();
            string query;

            string fields = "fields name, cover.url, rating, aggregated_rating, total_rating, category, version_parent";
            string baseFilter = "where parent_game = null";

            // --- 1. DYNAMICZNE SORTOWANIE API (Dla pobierania danych) ---
            string sortField = "total_rating";

            if (showCritic && !showUser) sortField = "aggregated_rating";
            else if (showUser && !showCritic) sortField = "rating";

            if (!string.IsNullOrEmpty(safeSearch) && mode == "browse")
            {
                query = $"{fields}; search \"{safeSearch}\"; {baseFilter} & cover.url != null; limit 100; offset {offset};";
            }
            else
            {
                query = $"{fields}; sort {sortField} desc; {baseFilter} & {sortField} != null & cover.url != null; limit 100; offset {offset};";
            }

            var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);

            var gamesFromApi = string.IsNullOrEmpty(jsonResponse)
                ? new List<ApiGame>()
                : JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse) ?? new List<ApiGame>();

            // 2. Filtrowanie (DLC itp.)
            var filteredGames = gamesFromApi
                .Where(g =>
                    g.Category == 0 &&
                    g.Version_parent == null &&
                    !string.IsNullOrEmpty(g.Name) &&
                    !g.Name.Contains("Bundle", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.Contains("Collection", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.Contains("Anthology", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.EndsWith("Pack", StringComparison.OrdinalIgnoreCase)
                )
                .Take(PageSize)
                .ToList();

            // 3. Pobranie ocen lokalnych
            Dictionary<long, double> localAverages = new Dictionary<long, double>();
            if (showLocal)
            {
                var gameIds = filteredGames.Select(g => g.Id).ToList();
                var localRatesData = await _context.GameRates
                    .Where(r => gameIds.Contains(r.IgdbGameId))
                    .Select(r => new { r.IgdbGameId, r.Value })
                    .ToListAsync();

                localAverages = localRatesData
                    .GroupBy(r => r.IgdbGameId)
                    .ToDictionary(g => g.Key, g => g.Average(r => r.Value));
            }

            // 4. Budowanie ViewModelu
            var finalGamesList = filteredGames.Select(apiGame =>
            {
                double sum = 0;
                int count = 0;

                // A. Ocena Graczy IGDB
                if (showUser && apiGame.Rating.HasValue)
                {
                    sum += apiGame.Rating.Value;
                    count++;
                }

                // B. Ocena Krytyków IGDB
                if (showCritic && apiGame.Aggregated_rating.HasValue)
                {
                    sum += apiGame.Aggregated_rating.Value;
                    count++;
                }

                // C. Ocena Lokalna
                if (showLocal && localAverages.ContainsKey(apiGame.Id))
                {
                    double localAvg = localAverages[apiGame.Id];
                    sum += (localAvg * 10);
                    count++;
                }

                double? finalRating = count > 0 ? sum / count : null;

                return new ApiGame
                {
                    Id = apiGame.Id,
                    Name = apiGame.Name,
                    Total_rating = finalRating,
                    Cover = apiGame.Cover != null ? new ApiCover { Url = apiGame.Cover.Url.Replace("t_thumb", "t_cover_big") } : null
                };
            })
            // --- NOWOŚĆ: SORTOWANIE W PAMIĘCI ---
            // Sortujemy tak, aby gry z najwyższą WIDOCZNĄ oceną były na górze.
            // Gry bez oceny (null) trafią na sam dół (-1).
            .OrderByDescending(g => g.Total_rating ?? -1)
            .ToList();
            // -------------------------------------

            if (mode == "guides") ViewData["Title"] = "Guides & Tips - Wybierz grę";
            else ViewData["Title"] = "Przeglądaj Gry";

            var viewModel = new Models.Modules.Games.GameBrowserViewModel
            {
                Games = finalGamesList,
                CurrentPage = page,
                SearchString = searchString,
                Mode = mode,
                ShowIgdbUser = showUser,
                ShowIgdbCritic = showCritic,
                ShowLocal = showLocal
            };

            return View(viewModel);
        }

        // GET: Games/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            var currentUserId = GetCurrentUserId();

            // Sprawdzamy czy gra jest w bibliotece (przekierowanie)
            if (currentUserId != Guid.Empty)
            {
                var gameInLibrary = await _context.GamesInLibraries
                    .FirstOrDefaultAsync(g => g.UserId == currentUserId && g.IgdbGameId == id);

                if (gameInLibrary != null)
                {
                    return RedirectToAction("Details", "Library", new { id = gameInLibrary.Id });
                }
            }

            // 1. Pobieranie danych z IGDB - DODANO POLA OCEN
            var gameQuery = $"fields name, cover.url, genres.name, involved_companies.company.name, involved_companies.developer, release_dates.human, rating, aggregated_rating; where id = {id}; limit 1;";
            var gameJsonResponse = await _igdbClient.ApiRequestAsync("games", gameQuery);

            if (string.IsNullOrEmpty(gameJsonResponse)) return NotFound();

            var gameDetailsFromApi = (JsonConvert.DeserializeObject<List<ApiGame>>(gameJsonResponse) ?? new List<ApiGame>()).FirstOrDefault();

            if (gameDetailsFromApi == null) return NotFound();

            // 2. Pobieranie ocen LOKALNYCH (GameRates)
            var localRates = await _context.GameRates.Where(r => r.IgdbGameId == id).ToListAsync();

            double localAvg = localRates.Any() ? localRates.Average(r => r.Value) : 0;
            int localCount = localRates.Count;
            double personalRating = 0;

            if (currentUserId != Guid.Empty)
            {
                var myRate = localRates.FirstOrDefault(r => r.UserId == currentUserId);
                if (myRate != null) personalRating = myRate.Value;
            }

            // 3. Budowanie modelu ocen
            var ratingsModel = new praca_dyplomowa_zesp.Models.ViewModels.GameRatingViewModel
            {
                IgdbGameId = gameDetailsFromApi.Id,
                IgdbUserRating = gameDetailsFromApi.Rating ?? 0,
                IgdbCriticRating = gameDetailsFromApi.Aggregated_rating ?? 0,
                LocalAverageRating = localAvg,
                LocalRatingCount = localCount,
                UserPersonalRating = personalRating
            };

            var viewModel = new Models.Modules.Games.GameDetailViewModel
            {
                IgdbGameId = gameDetailsFromApi.Id,
                Name = gameDetailsFromApi.Name,
                CoverUrl = gameDetailsFromApi.Cover?.Url?.Replace("t_thumb", "t_cover_big"),
                Genres = gameDetailsFromApi.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                Developer = gameDetailsFromApi.Involved_companies?.FirstOrDefault(ic => ic.developer)?.Company?.Name,
                ReleaseDate = gameDetailsFromApi.Release_dates?.FirstOrDefault()?.Human,
                Ratings = ratingsModel // <--- PRZYPISANIE OCEN
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

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RateGame([FromBody] GameRateRequest request)
        {
            if (request == null) return BadRequest();

            // Walidacja zakresu oceny
            double ratingValue = request.RatingValue;
            if (ratingValue < 0.5) ratingValue = 0.5; // Minimum pół gwiazdki
            if (ratingValue > 10) ratingValue = 10;   // Maximum 10 gwiazdek

            // Zaokrąglanie do 0.5
            ratingValue = Math.Round(ratingValue * 2, MidpointRounding.AwayFromZero) / 2;

            var userId = GetCurrentUserId();

            var existingRate = await _context.GameRates
                .FirstOrDefaultAsync(r => r.IgdbGameId == request.GameId && r.UserId == userId);

            if (existingRate != null)
            {
                existingRate.Value = ratingValue;
                _context.GameRates.Update(existingRate);
            }
            else
            {
                var rate = new Models.Interactions.Rates.GameRate
                {
                    UserId = userId,
                    IgdbGameId = request.GameId,
                    Value = ratingValue,
                    CreatedAt = DateTime.Now
                };
                _context.GameRates.Add(rate);
            }

            await _context.SaveChangesAsync();

            // Przeliczanie nowej średniej do zwrócenia w JSON
            var allRates = await _context.GameRates.Where(r => r.IgdbGameId == request.GameId).ToListAsync();
            var newAverage = allRates.Any() ? allRates.Average(r => r.Value) : ratingValue;
            var newCount = allRates.Count;

            return Ok(new { success = true, newAverage = newAverage, newCount = newCount });
        }

        public class GameRateRequest
        {
            public long GameId { get; set; }
            public double RatingValue { get; set; }
        }
    }
}