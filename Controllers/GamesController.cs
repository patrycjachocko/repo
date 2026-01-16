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
    /// <summary>
    /// Kontroler odpowiedzialny za przeglądanie bazy gier (IGDB), wyświetlanie szczegółów 
    /// oraz obsługę systemu ocen i recenzji.
    /// </summary>
    [AllowAnonymous]
    public class GamesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IGDBClient _igdbClient;
        private readonly UserManager<User> _userManager;

        public GamesController(ApplicationDbContext context, IGDBClient igdbClient, UserManager<User> userManager)
        {
            _context = context;
            _igdbClient = igdbClient;
            _userManager = userManager;
        }

        #region Game Browsing

        /// <summary>
        /// Wyświetla listę gier pobraną z API IGDB z uwzględnieniem filtrów ocen i wyszukiwania.
        /// </summary>
        public async Task<IActionResult> Index(
            string? searchString,
            int page = 1,
            int pageSize = 60,
            string mode = "browse",
            bool isFiltered = false,
            bool showUser = false,
            bool showCritic = false,
            bool showLocal = false)
        {
            // Walidacja rozmiaru strony
            if (pageSize != 36 && pageSize != 48 && pageSize != 60)
            {
                pageSize = 60;
            }

            // Domyślne ustawienia filtrów przy pierwszym wejściu
            if (!isFiltered)
            {
                showUser = showCritic = showLocal = true;
            }

            if (page < 1) page = 1;
            int offset = (page - 1) * pageSize;

            string safeSearch = searchString?.Replace("\"", "").Trim();
            string fields = "fields name, cover.url, rating, aggregated_rating, total_rating, category, version_parent";
            string baseFilter = "where parent_game = null";

            // Określenie priorytetu sortowania w API
            string sortField = "total_rating";
            if (showCritic && !showUser) sortField = "aggregated_rating";
            else if (showUser && !showCritic) sortField = "rating";

            // Pobieranie danych z lekkim nadmiarem dla bufora filtrowania in-memory
            int apiLimit = pageSize + 40;
            string query = string.IsNullOrEmpty(safeSearch)
                ? $"{fields}; sort {sortField} desc; {baseFilter} & {sortField} != null & cover.url != null; limit {apiLimit}; offset {offset};"
                : $"{fields}; search \"{safeSearch}\"; {baseFilter} & cover.url != null; limit {apiLimit}; offset {offset};";

            var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);
            var gamesFromApi = string.IsNullOrEmpty(jsonResponse)
                ? new List<ApiGame>()
                : JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse) ?? new List<ApiGame>();

            // Filtrowanie treści (pomijanie dodatków, paczek itp.)
            var filteredGames = gamesFromApi
                .Where(g => g.Category == 0 && g.Version_parent == null && !string.IsNullOrEmpty(g.Name) &&
                            !g.Name.Contains("Bundle", StringComparison.OrdinalIgnoreCase) &&
                            !g.Name.Contains("Collection", StringComparison.OrdinalIgnoreCase) &&
                            !g.Name.Contains("Anthology", StringComparison.OrdinalIgnoreCase) &&
                            !g.Name.EndsWith("Pack", StringComparison.OrdinalIgnoreCase))
                .Take(pageSize)
                .ToList();

            // Pobieranie średnich ocen z lokalnej bazy danych
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

            // Mapowanie wyników i obliczanie wypadkowej oceny
            var finalGamesList = filteredGames.Select(apiGame =>
            {
                double sum = 0;
                int count = 0;

                if (showUser && apiGame.Rating.HasValue) { sum += apiGame.Rating.Value; count++; }
                if (showCritic && apiGame.Aggregated_rating.HasValue) { sum += apiGame.Aggregated_rating.Value; count++; }
                if (showLocal && localAverages.ContainsKey(apiGame.Id)) { sum += (localAverages[apiGame.Id] * 10); count++; }

                return new ApiGame
                {
                    Id = apiGame.Id,
                    Name = apiGame.Name,
                    Total_rating = count > 0 ? sum / count : null,
                    Cover = apiGame.Cover != null ? new ApiCover { Url = apiGame.Cover.Url.Replace("t_thumb", "t_cover_big") } : null
                };
            }).OrderByDescending(g => g.Total_rating ?? -1).ToList();

            ViewData["Title"] = mode == "guides" ? "Guides & Tips - Wybierz grę" : "Przeglądaj Gry";

            return View(new GameBrowserViewModel
            {
                Games = finalGamesList,
                CurrentPage = page,
                SearchString = searchString,
                Mode = mode,
                ShowIgdbUser = showUser,
                ShowIgdbCritic = showCritic,
                ShowLocal = showLocal
            });
        }

        #endregion

        #region Game Details

        /// <summary>
        /// Wyświetla szczegółowe informacje o grze, w tym oceny użytkowników i topowe recenzje.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Details(long id)
        {
            var currentUserId = GetCurrentUserId();

            // Jeśli użytkownik posiada grę w bibliotece, przekieruj do widoku biblioteki
            if (currentUserId != Guid.Empty)
            {
                var gameInLibrary = await _context.GamesInLibraries
                    .FirstOrDefaultAsync(g => g.UserId == currentUserId && g.IgdbGameId == id);

                if (gameInLibrary != null)
                    return RedirectToAction("Details", "Library", new { id = gameInLibrary.Id });
            }

            var gameQuery = $"fields name, cover.url, genres.name, involved_companies.company.name, involved_companies.developer, release_dates.human, rating, aggregated_rating; where id = {id}; limit 1;";
            var gameJsonResponse = await _igdbClient.ApiRequestAsync("games", gameQuery);

            if (string.IsNullOrEmpty(gameJsonResponse)) return NotFound();

            var gameApiData = (JsonConvert.DeserializeObject<List<ApiGame>>(gameJsonResponse) ?? new List<ApiGame>()).FirstOrDefault();
            if (gameApiData == null) return NotFound();

            // Przetwarzanie ocen lokalnych
            var localRates = await _context.GameRates.Where(r => r.IgdbGameId == id).ToListAsync();
            double personalRating = currentUserId != Guid.Empty
                ? localRates.FirstOrDefault(r => r.UserId == currentUserId)?.Value ?? 0
                : 0;

            // Pobieranie i sortowanie recenzji (Najlepszy wynik Reactions -> Najnowsze)
            var reviews = await _context.GameReviews
                .Include(r => r.User)
                .Include(r => r.Reactions)
                .Where(r => r.IgdbGameId == id)
                .ToListAsync();

            var topReviews = reviews
                .Select(r => new {
                    Review = r,
                    Score = r.Reactions.Count(x => x.Type == ReactionType.Like) - r.Reactions.Count(x => x.Type == ReactionType.Dislike)
                })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Review.CreatedAt)
                .Take(3)
                .Select(x => x.Review)
                .ToList();

            return View(new GameDetailViewModel
            {
                IgdbGameId = gameApiData.Id,
                Name = gameApiData.Name,
                CoverUrl = gameApiData.Cover?.Url?.Replace("t_thumb", "t_cover_big"),
                Genres = gameApiData.Genres?.Select(g => g.Name).ToList() ?? new List<string>(),
                Developer = gameApiData.Involved_companies?.FirstOrDefault(ic => ic.developer)?.Company?.Name,
                ReleaseDate = gameApiData.Release_dates?.FirstOrDefault()?.Human,
                TopReviews = topReviews,
                Ratings = new GameRatingViewModel
                {
                    IgdbGameId = gameApiData.Id,
                    IgdbUserRating = gameApiData.Rating ?? 0,
                    IgdbCriticRating = gameApiData.Aggregated_rating ?? 0,
                    LocalAverageRating = localRates.Any() ? localRates.Average(r => r.Value) : 0,
                    LocalRatingCount = localRates.Count,
                    UserPersonalRating = personalRating
                }
            });
        }

        #endregion

        #region Steam Integration

        /// <summary>
        /// Obsługuje przekierowanie z importu Steam, próbując dopasować grę do bazy IGDB.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SteamRedirect(string query)
        {
            if (string.IsNullOrEmpty(query)) return RedirectToAction("Index");

            string cleanName = CleanSteamGameName(query);
            var igdbQuery = $"fields id, name, category, version_parent, aggregated_rating; search \"{cleanName}\"; where parent_game = null; limit 10;";

            try
            {
                var jsonResponse = await _igdbClient.ApiRequestAsync("games", igdbQuery);
                var games = JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse) ?? new List<ApiGame>();

                var validCandidates = games.Where(g =>
                    g.Category == 0 && g.Version_parent == null && !string.IsNullOrEmpty(g.Name) &&
                    !g.Name.Contains("Bundle", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.Contains("Collection", StringComparison.OrdinalIgnoreCase)).ToList();

                if (validCandidates.Any())
                {
                    var bestMatch = validCandidates.FirstOrDefault(g => g.Aggregated_rating != null) ?? validCandidates.First();
                    return RedirectToAction("Details", new { id = bestMatch.Id });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Steam Redirect Error: {ex.Message}");
            }

            TempData["ErrorMessage"] = $"Nie udało się automatycznie dopasować gry '{cleanName}'. Spróbuj wyszukać ją ręcznie.";
            return RedirectToAction("Index", new { searchString = cleanName });
        }

        #endregion

        #region Rating Actions (AJAX)

        /// <summary>
        /// Zapisuje lub aktualizuje ocenę gry wystawioną przez zalogowanego użytkownika.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RateGame([FromBody] GameRateRequest request)
        {
            if (request == null) return BadRequest();

            double val = Math.Clamp(request.RatingValue, 0.5, 10);
            val = Math.Round(val * 2, MidpointRounding.AwayFromZero) / 2;

            var userId = GetCurrentUserId();
            var existingRate = await _context.GameRates
                .FirstOrDefaultAsync(r => r.IgdbGameId == request.GameId && r.UserId == userId);

            if (existingRate != null)
            {
                existingRate.Value = val;
                _context.GameRates.Update(existingRate);
            }
            else
            {
                _context.GameRates.Add(new Models.Interactions.Rates.GameRate
                {
                    UserId = userId,
                    IgdbGameId = request.GameId,
                    Value = val,
                    CreatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();

            var allRates = await _context.GameRates.Where(r => r.IgdbGameId == request.GameId).ToListAsync();
            return Ok(new { success = true, newAverage = allRates.Average(r => r.Value), newCount = allRates.Count });
        }

        /// <summary>
        /// Usuwa ocenę użytkownika oraz powiązane z nią recenzje dla danej gry.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveRating([FromBody] RemoveRateRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty) return Json(new { success = false });

            var rating = await _context.GameRates
                .FirstOrDefaultAsync(r => r.UserId == userId && r.IgdbGameId == request.IgdbGameId);

            if (rating != null) _context.GameRates.Remove(rating);

            var reviews = await _context.GameReviews
                .Where(r => r.UserId == userId && r.IgdbGameId == request.IgdbGameId)
                .ToListAsync();

            if (reviews.Any()) _context.GameReviews.RemoveRange(reviews);

            await _context.SaveChangesAsync();

            var allRatings = await _context.GameRates.Where(r => r.IgdbGameId == request.IgdbGameId).ToListAsync();
            return Json(new { success = true, newAverage = allRatings.Any() ? allRatings.Average(r => r.Value) : 0 });
        }

        #endregion

        #region Helpers

        private Guid GetCurrentUserId()
        {
            var userIdString = _userManager.GetUserId(User);
            return Guid.TryParse(userIdString, out Guid userId) ? userId : Guid.Empty;
        }

        private string CleanSteamGameName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Replace("\"", "").Replace("™", "").Replace("®", "").Replace("©", "").Trim();
        }

        public class GameRateRequest
        {
            public long GameId { get; set; }
            public double RatingValue { get; set; }
        }

        #endregion
    }
}