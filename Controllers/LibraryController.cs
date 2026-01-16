using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using praca_dyplomowa.Data;
using praca_dyplomowa_zesp.Models.API;
using praca_dyplomowa_zesp.Models.Modules.Libraries;
using praca_dyplomowa_zesp.Models.Modules.Libraries.UserLibrary;
using praca_dyplomowa_zesp.Models.Users;
using praca_dyplomowa_zesp.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace praca_dyplomowa_zesp.Controllers
{
    /// <summary>
    /// Kontroler zarządzający osobistą biblioteką gier użytkownika.
    /// Obsługuje przeglądanie, szczegóły, postępy oraz listę zadań (To-Do).
    /// </summary>
    [Authorize]
    public class LibraryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IGDBClient _igdbClient;
        private readonly UserManager<User> _userManager;
        private readonly SteamApiService _steamService;

        private const int PageSize = 60;

        public LibraryController(
            ApplicationDbContext context,
            IGDBClient igdbClient,
            UserManager<User> userManager,
            SteamApiService steamService)
        {
            _context = context;
            _igdbClient = igdbClient;
            _userManager = userManager;
            _steamService = steamService;
        }

        #region Core Actions

        /// <summary>
        /// Wyświetla listę gier w bibliotece użytkownika z uwzględnieniem filtrów i paginacji.
        /// </summary>
        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Index(string? searchString, string statusFilter = "all", int page = 1)
        {
            var currentUserId = GetCurrentUserId();

            // Pobranie bazowych danych o grach użytkownika
            var query = _context.GamesInLibraries
                .Where(g => g.UserId == currentUserId)
                .OrderByDescending(g => g.LastAccessed)
                .AsQueryable();

            // Filtrowanie po statusie ukończenia
            query = statusFilter switch
            {
                "completed" => query.Where(g => g.CurrentUserStoryProgressPercent == 100),
                "playing" => query.Where(g => g.CurrentUserStoryProgressPercent > 0 && g.CurrentUserStoryProgressPercent < 100),
                "toplay" => query.Where(g => g.CurrentUserStoryProgressPercent == 0),
                _ => query
            };

            var filteredDbList = await query.ToListAsync();
            var finalGamesList = new List<MainLibraryViewModel>();
            int totalGames;

            if (!string.IsNullOrEmpty(searchString))
            {
                // Scenariusz wyszukiwania: pobieramy dane API dla wszystkich gier, aby przefiltrować po nazwie
                var allIgdbIds = filteredDbList.Select(g => g.IgdbGameId).Distinct().ToList();
                var allApiGames = await FetchApiGamesInBatches(allIgdbIds);

                var allViewModels = filteredDbList.Select(dbGame => {
                    var apiGame = allApiGames.FirstOrDefault(a => a.Id == dbGame.IgdbGameId);
                    return MapToMainLibraryViewModel(dbGame, apiGame);
                }).Where(g => g.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase)).ToList();

                totalGames = allViewModels.Count;
                finalGamesList = allViewModels.Skip((page - 1) * PageSize).Take(PageSize).ToList();
            }
            else
            {
                // Scenariusz optymalny: pobieramy dane API tylko dla gier na bieżącej stronie
                totalGames = filteredDbList.Count;
                var pagedDbList = filteredDbList.Skip((page - 1) * PageSize).Take(PageSize).ToList();
                var pageIgdbIds = pagedDbList.Select(g => g.IgdbGameId).Distinct().ToList();

                var pageApiGames = new List<ApiGame>();
                if (pageIgdbIds.Any())
                {
                    var idsString = string.Join(",", pageIgdbIds);
                    var queryApi = $"fields name, cover.url; where id = ({idsString}); limit 100;";
                    pageApiGames = await ExecuteIgdbQuery(queryApi);
                }

                finalGamesList = pagedDbList.Select(dbGame => {
                    var apiGame = pageApiGames.FirstOrDefault(a => a.Id == dbGame.IgdbGameId);
                    return MapToMainLibraryViewModel(dbGame, apiGame);
                }).ToList();
            }

            return View(new UserLibraryIndexViewModel
            {
                Games = finalGamesList,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalGames / (double)PageSize),
                SearchString = searchString,
                StatusFilter = statusFilter
            });
        }

        /// <summary>
        /// Wyświetla szczegółowe informacje o grze z biblioteki, w tym osiągnięcia i statystyki.
        /// </summary>
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            var user = await _userManager.GetUserAsync(User);

            var gameFromDb = await _context.GamesInLibraries
                .Include(g => g.ToDoItems)
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == currentUserId);

            if (gameFromDb == null) return NotFound();

            // Pobieranie rozszerzonych danych o grze z IGDB
            var gameQuery = $"fields name, cover.url, genres.name, involved_companies.company.name, involved_companies.developer, release_dates.human, websites.url, websites.category, external_games.category, external_games.uid, rating, aggregated_rating; where id = {gameFromDb.IgdbGameId}; limit 1;";
            var apiGames = await ExecuteIgdbQuery(gameQuery);
            var gameDetailsFromApi = apiGames.FirstOrDefault();

            // Przetwarzanie ocen
            var localRates = await _context.GameRates.Where(r => r.IgdbGameId == gameFromDb.IgdbGameId).ToListAsync();
            var ratingsModel = new GameRatingViewModel
            {
                IgdbGameId = gameFromDb.IgdbGameId,
                IgdbUserRating = gameDetailsFromApi?.Rating ?? 0,
                IgdbCriticRating = gameDetailsFromApi?.Aggregated_rating ?? 0,
                LocalAverageRating = localRates.Any() ? localRates.Average(r => r.Value) : 0,
                LocalRatingCount = localRates.Count,
                UserPersonalRating = localRates.FirstOrDefault(r => r.UserId == currentUserId)?.Value ?? 0
            };

            // Obsługa integracji ze Steam (AppID i osiągnięcia)
            string steamAppId = await ResolveSteamAppId(gameDetailsFromApi);
            var achievementsData = await ProcessAchievements(currentUserId, user?.SteamId, gameFromDb.IgdbGameId, steamAppId);

            // Aktualizacja czasu ostatniego dostępu i ewentualnie postępu
            gameFromDb.LastAccessed = DateTime.Now;
            if (achievementsData.FinalAchievements.Any())
            {
                gameFromDb.CurrentUserStoryProgressPercent = achievementsData.CalculatedProgress;
            }

            _context.Update(gameFromDb);
            await _context.SaveChangesAsync();

            var viewModel = new GameInLibraryViewModel
            {
                DbId = gameFromDb.Id,
                IgdbGameId = gameFromDb.IgdbGameId,
                UserId = gameFromDb.UserId,
                Name = gameDetailsFromApi?.Name ?? "Brak nazwy",
                CoverUrl = gameDetailsFromApi?.Cover?.Url?.Replace("t_thumb", "t_cover_big"),
                Genres = gameDetailsFromApi?.Genres?.Select(g => g.Name).ToList(),
                Developer = gameDetailsFromApi?.Involved_companies?.FirstOrDefault(ic => ic.developer)?.Company?.Name,
                ReleaseDate = gameDetailsFromApi?.Release_dates?.FirstOrDefault()?.Human,
                DateAddedToLibrary = gameFromDb.DateAddedToLibrary,
                CurrentUserStoryMission = gameFromDb.CurrentUserStoryMission,
                CurrentUserStoryProgressPercent = gameFromDb.CurrentUserStoryProgressPercent,
                Notes = gameFromDb.Notes,
                Achievements = achievementsData.FinalAchievements,
                IsSteamConnected = !string.IsNullOrEmpty(user?.SteamId),
                IsSteamGame = !string.IsNullOrEmpty(steamAppId),
                SteamUnlockedAchievementIds = achievementsData.SteamUnlockedIds,
                ToDoItems = gameFromDb.ToDoItems.ToList(),
                Ratings = ratingsModel
            };

            return View(viewModel);
        }

        #endregion

        #region Library Management

        public IActionResult Create() => View();

        /// <summary>
        /// Dodaje nową grę do biblioteki użytkownika.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IgdbGameId")] GameInLibrary gameInLibrary, string returnUrl)
        {
            var currentUserId = GetCurrentUserId();
            gameInLibrary.UserId = currentUserId;
            gameInLibrary.DateAddedToLibrary = DateTime.Now;
            gameInLibrary.LastAccessed = DateTime.Now;

            try
            {
                var query = $"fields cover.url; where id = {gameInLibrary.IgdbGameId};";
                var gamesInfo = await ExecuteIgdbQuery(query);
                var gameInfo = gamesInfo?.FirstOrDefault();

                // Walidacja istnienia okładki
                if (gameInfo?.Cover == null || string.IsNullOrEmpty(gameInfo.Cover.Url))
                {
                    return RedirectToLocal(returnUrl);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas dodawania do biblioteki: {ex.Message}");
            }

            bool alreadyExists = await _context.GamesInLibraries.AnyAsync(g => g.UserId == currentUserId && g.IgdbGameId == gameInLibrary.IgdbGameId);
            if (!alreadyExists)
            {
                _context.Add(gameInLibrary);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Details), new { id = gameInLibrary.Id });
            }

            var existingGame = await _context.GamesInLibraries.FirstOrDefaultAsync(g => g.UserId == currentUserId && g.IgdbGameId == gameInLibrary.IgdbGameId);
            return existingGame != null
                ? RedirectToAction(nameof(Details), new { id = existingGame.Id })
                : RedirectToLocal(returnUrl);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var currentUserId = GetCurrentUserId();
            var game = await _context.GamesInLibraries.FirstOrDefaultAsync(m => m.Id == id && m.UserId == currentUserId);

            if (game == null) return NotFound();
            return View(game);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var currentUserId = GetCurrentUserId();
            var game = await _context.GamesInLibraries.FindAsync(id);

            if (game != null && game.UserId == currentUserId)
            {
                var achievements = await _context.UserAchievements
                    .Where(ua => ua.UserId == currentUserId && ua.IgdbGameId == game.IgdbGameId)
                    .ToListAsync();

                _context.UserAchievements.RemoveRange(achievements);
                _context.GamesInLibraries.Remove(game);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Czyści bibliotekę użytkownika na podstawie aktywnych filtrów wyszukiwania.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearLibrary(string? searchString, string statusFilter = "all")
        {
            var currentUserId = GetCurrentUserId();
            var query = _context.GamesInLibraries.Where(g => g.UserId == currentUserId).AsQueryable();

            if (statusFilter == "completed") query = query.Where(g => g.CurrentUserStoryProgressPercent == 100);
            else if (statusFilter == "playing") query = query.Where(g => g.CurrentUserStoryProgressPercent > 0 && g.CurrentUserStoryProgressPercent < 100);
            else if (statusFilter == "toplay") query = query.Where(g => g.CurrentUserStoryProgressPercent == 0);

            List<GameInLibrary> gamesToDelete;

            if (!string.IsNullOrEmpty(searchString))
            {
                var potentialGames = await query.ToListAsync();
                var allIgdbIds = potentialGames.Select(g => g.IgdbGameId).Distinct().ToList();
                var allApiGames = await FetchApiGamesInBatches(allIgdbIds);

                gamesToDelete = potentialGames.Where(dbGame =>
                {
                    var apiInfo = allApiGames.FirstOrDefault(a => a.Id == dbGame.IgdbGameId);
                    return (apiInfo?.Name ?? "").Contains(searchString, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }
            else
            {
                gamesToDelete = await query.ToListAsync();
            }

            if (!gamesToDelete.Any())
            {
                TempData["ErrorMessage"] = "Nie znaleziono gier do usunięcia wg podanych kryteriów.";
                return RedirectToAction(nameof(Index), new { searchString, statusFilter });
            }

            var gamesIds = gamesToDelete.Select(g => g.IgdbGameId).ToList();
            var dbIds = gamesToDelete.Select(g => g.Id).ToList();

            var userAchievements = await _context.UserAchievements.Where(ua => ua.UserId == currentUserId && gamesIds.Contains(ua.IgdbGameId)).ToListAsync();
            var todoItems = await _context.ToDoItems.Where(t => dbIds.Contains(t.GameInLibraryId)).ToListAsync();

            _context.ToDoItems.RemoveRange(todoItems);
            _context.UserAchievements.RemoveRange(userAchievements);
            _context.GamesInLibraries.RemoveRange(gamesToDelete);

            await _context.SaveChangesAsync();
            TempData["StatusMessage"] = $"Usunięto {gamesToDelete.Count} gier z biblioteki.";

            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region AJAX Updates

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNotes(int id, string notes)
        {
            var game = await _context.GamesInLibraries.FirstOrDefaultAsync(g => g.Id == id && g.UserId == GetCurrentUserId());
            if (game == null) return Json(new { success = false, error = "Nie znaleziono gry." });

            game.Notes = notes;
            _context.Update(game);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProgress(int id, int percent)
        {
            var game = await _context.GamesInLibraries.FirstOrDefaultAsync(g => g.Id == id && g.UserId == GetCurrentUserId());
            if (game == null) return Json(new { success = false, error = "Nie znaleziono gry." });

            game.CurrentUserStoryProgressPercent = Math.Clamp(percent, 0, 100);
            _context.Update(game);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAchievement(long igdbGameId, string achievementExternalId)
        {
            if (string.IsNullOrEmpty(achievementExternalId)) return Json(new { success = false, error = "Brak ID." });

            var currentUserId = GetCurrentUserId();
            var achievement = await _context.UserAchievements.FirstOrDefaultAsync(ua =>
                ua.UserId == currentUserId && ua.IgdbGameId == igdbGameId && ua.AchievementExternalId == achievementExternalId);

            bool newStatus;
            if (achievement == null)
            {
                _context.UserAchievements.Add(new UserAchievement
                {
                    UserId = currentUserId,
                    IgdbGameId = igdbGameId,
                    AchievementExternalId = achievementExternalId,
                    IsUnlocked = true
                });
                newStatus = true;
            }
            else
            {
                achievement.IsUnlocked = !achievement.IsUnlocked;
                newStatus = achievement.IsUnlocked;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, isUnlocked = newStatus });
        }

        #endregion

        #region To-Do List AJAX

        [HttpPost]
        public async Task<IActionResult> AddToDoItem(int gameId, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return Json(new { success = false });

            var game = await _context.GamesInLibraries.FirstOrDefaultAsync(g => g.Id == gameId && g.UserId == GetCurrentUserId());
            if (game == null) return Json(new { success = false, message = "Gra nie znaleziona." });

            var newItem = new ToDoItem { Content = content, GameInLibraryId = gameId, IsCompleted = false };
            _context.ToDoItems.Add(newItem);
            await _context.SaveChangesAsync();

            return Json(new { success = true, id = newItem.Id, content = newItem.Content });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleToDoItem(int itemId)
        {
            var item = await _context.ToDoItems
                .Include(t => t.GameInLibrary)
                .FirstOrDefaultAsync(t => t.Id == itemId && t.GameInLibrary.UserId == GetCurrentUserId());

            if (item == null) return Json(new { success = false });

            item.IsCompleted = !item.IsCompleted;
            await _context.SaveChangesAsync();

            return Json(new { success = true, isCompleted = item.IsCompleted });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteToDoItem(int itemId)
        {
            var item = await _context.ToDoItems
                .Include(t => t.GameInLibrary)
                .FirstOrDefaultAsync(t => t.Id == itemId && t.GameInLibrary.UserId == GetCurrentUserId());

            if (item == null) return Json(new { success = false });

            _context.ToDoItems.Remove(item);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCompletedToDoItems(int gameId)
        {
            var itemsToDelete = await _context.ToDoItems
                .Where(t => t.GameInLibraryId == gameId && t.GameInLibrary.UserId == GetCurrentUserId() && t.IsCompleted)
                .ToListAsync();

            if (itemsToDelete.Any())
            {
                _context.ToDoItems.RemoveRange(itemsToDelete);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ResetToDoList(int gameId)
        {
            var items = await _context.ToDoItems
                .Where(t => t.GameInLibraryId == gameId && t.GameInLibrary.UserId == GetCurrentUserId())
                .ToListAsync();

            if (items.Any())
            {
                _context.ToDoItems.RemoveRange(items);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        #endregion

        #region Private Helpers

        private Guid GetCurrentUserId()
        {
            var userIdString = _userManager.GetUserId(User);
            return Guid.TryParse(userIdString, out Guid userId) ? userId : Guid.Empty;
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index", "Games", new { mode = "browse" });
        }

        private async Task<List<ApiGame>> ExecuteIgdbQuery(string query)
        {
            try
            {
                var json = await _igdbClient.ApiRequestAsync("games", query);
                if (string.IsNullOrEmpty(json)) return new List<ApiGame>();
                return JsonConvert.DeserializeObject<List<ApiGame>>(json) ?? new List<ApiGame>();
            }
            catch { return new List<ApiGame>(); }
        }

        private async Task<List<ApiGame>> FetchApiGamesInBatches(List<long> ids)
        {
            var allApiGames = new List<ApiGame>();
            for (int i = 0; i < ids.Count; i += 50)
            {
                var batchIds = ids.Skip(i).Take(50).ToList();
                var queryApi = $"fields name, cover.url; where id = ({string.Join(",", batchIds)}); limit 50;";
                allApiGames.AddRange(await ExecuteIgdbQuery(queryApi));
            }
            return allApiGames;
        }

        private MainLibraryViewModel MapToMainLibraryViewModel(GameInLibrary dbGame, ApiGame? apiGame)
        {
            return new MainLibraryViewModel
            {
                DbId = dbGame.Id,
                IgdbGameId = dbGame.IgdbGameId,
                Name = apiGame?.Name ?? "Nieznana",
                CoverUrl = apiGame?.Cover?.Url?.Replace("t_thumb", "t_cover_big") ?? "https://via.placeholder.com/264x352.png?text=Brak+okładki",
                ProgressPercent = dbGame.CurrentUserStoryProgressPercent
            };
        }

        private async Task<string?> ResolveSteamAppId(ApiGame? apiGame)
        {
            if (apiGame == null) return null;

            // 1. Sprawdzenie w external_games
            var steamExternal = apiGame.External_games?.FirstOrDefault(e => e.Category == 1);
            if (steamExternal != null) return steamExternal.Uid;

            // 2. Parsowanie z URL strony Steam
            var steamWebsite = apiGame.Websites?.FirstOrDefault(w => w.Category == 13);
            if (steamWebsite != null)
            {
                var match = Regex.Match(steamWebsite.Url, @"app/(\d+)");
                if (match.Success) return match.Groups[1].Value;
            }

            // 3. Wyszukiwanie przez serwis Steam
            return await _steamService.SearchAppIdAsync(apiGame.Name);
        }

        private async Task<(List<AchievementViewModel> FinalAchievements, List<string> SteamUnlockedIds, int CalculatedProgress)> ProcessAchievements(Guid userId, string? steamId, long igdbGameId, string? steamAppId)
        {
            var finalAchievements = new List<AchievementViewModel>();
            var steamUnlockedIds = new List<string>();
            int progress = 0;

            if (string.IsNullOrEmpty(steamAppId)) return (finalAchievements, steamUnlockedIds, progress);

            var steamSchema = await _steamService.GetSchemaForGameAsync(steamAppId);
            if (steamSchema != null && steamSchema.Any())
            {
                var localAchievements = await _context.UserAchievements
                    .Where(ua => ua.UserId == userId && ua.IgdbGameId == igdbGameId)
                    .ToListAsync();

                if (!string.IsNullOrEmpty(steamId))
                {
                    var playerAchievements = await _steamService.GetGameAchievementsAsync(steamId, steamAppId);
                    steamUnlockedIds = playerAchievements.Where(pa => pa.Achieved == 1).Select(pa => pa.ApiName).ToList();
                }

                finalAchievements = steamSchema.Select(schema => new AchievementViewModel
                {
                    Name = schema.DisplayName,
                    Description = schema.Description,
                    IconUrl = schema.Icon,
                    ExternalId = schema.ApiName,
                    IsUnlocked = steamUnlockedIds.Contains(schema.ApiName) ||
                                 localAchievements.Any(la => la.AchievementExternalId == schema.ApiName && la.IsUnlocked)
                }).ToList();

                if (finalAchievements.Any())
                {
                    progress = (int)Math.Round((double)finalAchievements.Count(a => a.IsUnlocked) / finalAchievements.Count * 100);
                }
            }

            return (finalAchievements, steamUnlockedIds, progress);
        }

        #endregion
    }
}