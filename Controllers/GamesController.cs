using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using praca_dyplomowa.Data;
using praca_dyplomowa_zesp.Models.API;
using praca_dyplomowa_zesp.Models.Interactions.Reactions;
using praca_dyplomowa_zesp.Models.Modules.Games;
using praca_dyplomowa_zesp.Models.Users;
using praca_dyplomowa_zesp.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            int pageSize = 60, // <--- 1. NOWE: Parametr rozmiaru strony
            string mode = "browse",
            bool isFiltered = false,
            bool showUser = false,
            bool showCritic = false,
            bool showLocal = false
        )
        {
            // 2. NOWE: Walidacja (Security Check)
            // Jeśli ktoś wpisze dziwną liczbę ręcznie w URL, ustawiamy na 60
            if (pageSize != 36 && pageSize != 48 && pageSize != 60)
            {
                pageSize = 60;
            }

            // LOGIKA DOMYŚLNYCH WARTOŚCI (Pierwsze wejście = wszystko włączone)
            if (!isFiltered)
            {
                showUser = true;
                showCritic = true;
                showLocal = true;
            }

            if (page < 1) page = 1;

            // 3. NOWE: Obliczanie offsetu na podstawie wybranego pageSize
            int offset = (page - 1) * pageSize;

            string safeSearch = searchString?.Replace("\"", "").Trim();
            string query;

            string fields = "fields name, cover.url, rating, aggregated_rating, total_rating, category, version_parent";
            string baseFilter = "where parent_game = null";

            // --- 1. DYNAMICZNE SORTOWANIE API (Dla pobierania danych) ---
            string sortField = "total_rating";

            if (showCritic && !showUser) sortField = "aggregated_rating";
            else if (showUser && !showCritic) sortField = "rating";

            // Limit w API ustawiamy na trochę więcej (np. 100), żeby mieć bufor na filtrowanie in-memory
            // (bo odrzucamy DLC, bundle itp., więc musimy pobrać więcej niż wyświetlamy)
            int apiLimit = pageSize + 40;

            if (!string.IsNullOrEmpty(safeSearch))
            {
                query = $"{fields}; search \"{safeSearch}\"; {baseFilter} & cover.url != null; limit {apiLimit}; offset {offset};";
            }
            else
            {
                query = $"{fields}; sort {sortField} desc; {baseFilter} & {sortField} != null & cover.url != null; limit {apiLimit}; offset {offset};";
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
                .Take(pageSize) // <--- 4. NOWE: Bierzemy tyle, ile wybrał użytkownik (36/48/60)
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
            // --- Sortowanie w pamięci ---
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
                // Jeśli w ViewModelu masz pole PageSize, możesz je tu przypisać:
                // PageSize = pageSize 
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

            // 4. NOWE: Pobieranie Top 3 Recenzji
            var reviews = await _context.GameReviews
                .Include(r => r.User)
                .Include(r => r.Reactions)
                .Where(r => r.IgdbGameId == id)
                .ToListAsync();

            // Sortowanie w pamięci:
            // 1. Najpierw według Wyniku (Like - Dislike) malejąco
            // 2. Jeśli wynik taki sam, to nowsze na górze
            var topReviews = reviews
                .Select(r => new
                {
                    Review = r,
                    Score = r.Reactions.Count(x => x.Type == ReactionType.Like) - r.Reactions.Count(x => x.Type == ReactionType.Dislike)
                })
                .OrderByDescending(x => x.Score)              // Najlepsza ocena
                .ThenByDescending(x => x.Review.CreatedAt)    // Najnowsze przy remisie
                .Take(3)
                .Select(x => x.Review)
                .ToList();

            var viewModel = new Models.Modules.Games.GameDetailViewModel
            {
                IgdbGameId = gameDetailsFromApi.Id,
                Name = gameDetailsFromApi.Name,
                CoverUrl = gameDetailsFromApi.Cover?.Url?.Replace("t_thumb", "t_cover_big"),
                Genres = gameDetailsFromApi.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                Developer = gameDetailsFromApi.Involved_companies?.FirstOrDefault(ic => ic.developer)?.Company?.Name,
                ReleaseDate = gameDetailsFromApi.Release_dates?.FirstOrDefault()?.Human,
                Ratings = ratingsModel, // <--- PRZYPISANIE OCEN
                TopReviews = topReviews // <--- PRZYPISANIE RECENZJI
            };

            return View(viewModel);
        }

        // Redirect ze Steam
        [HttpGet]
        public async Task<IActionResult> SteamRedirect(string query)
        {
            if (string.IsNullOrEmpty(query)) return RedirectToAction("Index");

            // 1. Użycie tego samego czyszczenia nazwy co przy imporcie
            var cleanName = CleanSteamGameName(query);

            // 2. Zapytanie identyczne jak w 'Etap 2' importu (z aggregated_rating)
            // Zmieniono limit na 10 (jak w imporcie), aby nie pobierać za dużo śmieci
            var igdbQuery = $"fields id, name, category, version_parent, aggregated_rating; search \"{cleanName}\"; where parent_game = null; limit 10;";

            try
            {
                var jsonResponse = await _igdbClient.ApiRequestAsync("games", igdbQuery);

                var games = string.IsNullOrEmpty(jsonResponse)
                    ? new List<ApiGame>()
                    : JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse) ?? new List<ApiGame>();

                // 3. Rozszerzone filtrowanie (takie samo jak w imporcie)
                var validCandidates = games.Where(g =>
                    g.Category == 0 &&
                    g.Version_parent == null &&
                    !string.IsNullOrEmpty(g.Name) &&
                    !g.Name.Contains("Bundle", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.Contains("Collection", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.Contains("Anthology", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.EndsWith("Pack", StringComparison.OrdinalIgnoreCase)
                ).ToList();

                if (validCandidates.Any())
                {
                    // 4. Wybór najlepszego kandydata (tego z oceną krytyków), jeśli istnieje
                    var bestMatch = validCandidates.FirstOrDefault(g => g.Aggregated_rating != null) ?? validCandidates.FirstOrDefault();

                    if (bestMatch != null)
                    {
                        return RedirectToAction("Details", new { id = bestMatch.Id });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas wyszukiwania gry ze Steam: {ex.Message}");
            }

            // Jeśli nie znaleziono idealnego dopasowania, przekieruj do wyszukiwarki z czystą nazwą
            TempData["ErrorMessage"] = $"Nie udało się automatycznie dopasować gry '{cleanName}'. Spróbuj znaleźć ją na liście poniżej.";
            return RedirectToAction("Index", new { searchString = cleanName });
        }

        // --- METODA POMOCNICZA SKOPIOWANA Z ProfileController ---
        private string CleanSteamGameName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            // Usuwanie znaków handlowych i cudzysłowów
            return name.Replace("\"", "").Replace("™", "").Replace("®", "").Replace("©", "").Trim();
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRating([FromBody] RemoveRateRequest request)
        {
            var userIdStr = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userIdStr)) return Json(new { success = false });
            var userId = Guid.Parse(userIdStr);

            // 1. Znajdź i usuń ocenę
            var rating = await _context.GameRates
                .FirstOrDefaultAsync(r => r.UserId == userId && r.IgdbGameId == request.IgdbGameId);

            if (rating != null)
            {
                _context.GameRates.Remove(rating);
            }

            // 2. Znajdź i usuń recenzje tego użytkownika dla tej gry
            // UWAGA: Upewnij się, że GameReview.IgdbGameId to właściwe pole. 
            // Jeśli w modelu recenzji używasz 'GameId' jako ID z IGDB, zostaw jak jest.
            var reviews = await _context.GameReviews
                .Where(r => r.UserId == userId && r.IgdbGameId == request.IgdbGameId)
                .ToListAsync();

            if (reviews.Any())
            {
                _context.GameReviews.RemoveRange(reviews);
            }

            await _context.SaveChangesAsync();

            // 3. Przelicz nową średnią
            var allRatings = await _context.GameRates.Where(r => r.IgdbGameId == request.IgdbGameId).ToListAsync();
            double newAverage = allRatings.Any() ? allRatings.Average(r => r.Value) : 0;

            return Json(new { success = true, newAverage = newAverage });
        }
    }
}