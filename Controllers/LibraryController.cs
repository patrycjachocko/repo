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
using praca_dyplomowa_zesp.Models.Modules.Libraries.UserLibrary;
using praca_dyplomowa_zesp.Models.Modules.Libraries;
using System.Text.RegularExpressions;

namespace praca_dyplomowa_zesp.Controllers
{
    [Authorize]
    public class LibraryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IGDBClient _igdbClient;
        private readonly UserManager<User> _userManager;
        private readonly SteamApiService _steamService;

        public LibraryController(ApplicationDbContext context, IGDBClient igdbClient, UserManager<User> userManager, SteamApiService steamService)
        {
            _context = context;
            _igdbClient = igdbClient;
            _userManager = userManager;
            _steamService = steamService;
        }

        private Guid GetCurrentUserId()
        {
            var userIdString = _userManager.GetUserId(User);
            if (Guid.TryParse(userIdString, out Guid userId)) return userId;
            return Guid.Empty;
        }

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Index(string? searchString, string statusFilter = "all")
        {
            var currentUserId = GetCurrentUserId();
            var userGamesFromDb = await _context.GamesInLibraries
                .Where(g => g.UserId == currentUserId)
                .OrderByDescending(g => g.LastAccessed)
                .ToListAsync();

            if (!userGamesFromDb.Any()) return View(new List<MainLibraryViewModel>());

            // --- Pobieranie danych z IGDB (bez zmian) ---
            var allApiGames = new List<ApiGame>();
            var allIgdbIds = userGamesFromDb.Select(g => g.IgdbGameId).Distinct().ToList();
            int batchSize = 50;
            for (int i = 0; i < allIgdbIds.Count; i += batchSize)
            {
                var batchIds = allIgdbIds.Skip(i).Take(batchSize).ToList();
                var idsString = string.Join(",", batchIds);
                var query = $"fields name, cover.url; where id = ({idsString}); limit {batchSize};";
                try
                {
                    var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);
                    if (!string.IsNullOrEmpty(jsonResponse))
                    {
                        var chunkGames = JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse);
                        if (chunkGames != null) allApiGames.AddRange(chunkGames);
                    }
                }
                catch (Exception ex) { Console.WriteLine($"Błąd index: {ex.Message}"); }
            }

            // --- Mapowanie na ViewModel (TERAZ Z PROCENTAMI) ---
            var viewModels = userGamesFromDb.Select(dbGame =>
            {
                var apiGame = allApiGames.FirstOrDefault(apiG => apiG.Id == dbGame.IgdbGameId);
                return new MainLibraryViewModel
                {
                    DbId = dbGame.Id,
                    IgdbGameId = dbGame.IgdbGameId,
                    Name = apiGame?.Name ?? "Wczytywanie...",
                    CoverUrl = apiGame?.Cover?.Url?.Replace("t_thumb", "t_cover_big") ?? "https://via.placeholder.com/264x352.png?text=Brak+okładki",
                    ProgressPercent = dbGame.CurrentUserStoryProgressPercent // <--- Przypisujemy procenty z bazy
                };
            }).ToList();

            // --- WYSZUKIWANIE ---
            if (!string.IsNullOrEmpty(searchString))
            {
                viewModels = viewModels
                    .Where(g => g.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // --- FILTROWANIE PO STATUSIE (Nowość) ---
            switch (statusFilter)
            {
                case "completed": // Ukończone (100%)
                    viewModels = viewModels.Where(g => g.ProgressPercent == 100).ToList();
                    break;
                case "playing": // W trakcie (1-99%)
                    viewModels = viewModels.Where(g => g.ProgressPercent > 0 && g.ProgressPercent < 100).ToList();
                    break;
                case "toplay": // Do zagrania (0%)
                    viewModels = viewModels.Where(g => g.ProgressPercent == 0).ToList();
                    break;
            }

            // Przekazujemy parametry do widoku, aby zachować stan filtrów
            ViewData["SearchString"] = searchString;
            ViewData["StatusFilter"] = statusFilter;

            return View(viewModels);
        }

        // GET: Library/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            var user = await _userManager.GetUserAsync(User);

            var gameFromDb = await _context.GamesInLibraries
                .Include(g => g.ToDoItems)
                .FirstOrDefaultAsync(m => m.Id == id && m.UserId == currentUserId);
            if (gameFromDb == null) return NotFound();

            // 1. Pobierz dane gry z IGDB - DODANO rating i aggregated_rating
            var gameQuery = $"fields name, cover.url, genres.name, involved_companies.company.name, involved_companies.developer, release_dates.human, websites.url, websites.category, external_games.category, external_games.uid, rating, aggregated_rating; where id = {gameFromDb.IgdbGameId}; limit 1;";
            var gameJsonResponse = await _igdbClient.ApiRequestAsync("games", gameQuery);

            ApiGame gameDetailsFromApi = null;
            if (!string.IsNullOrEmpty(gameJsonResponse))
            {
                gameDetailsFromApi = (JsonConvert.DeserializeObject<List<ApiGame>>(gameJsonResponse) ?? new List<ApiGame>()).FirstOrDefault();
            }

            // --- LOGIKA OCEN ---
            var localRates = await _context.GameRates.Where(r => r.IgdbGameId == gameFromDb.IgdbGameId).ToListAsync();
            double localAvg = localRates.Any() ? localRates.Average(r => r.Value) : 0;
            int localCount = localRates.Count;
            double personalRating = 0;

            var myRate = localRates.FirstOrDefault(r => r.UserId == currentUserId);
            if (myRate != null) personalRating = myRate.Value;

            var ratingsModel = new praca_dyplomowa_zesp.Models.ViewModels.GameRatingViewModel
            {
                IgdbGameId = gameFromDb.IgdbGameId,
                IgdbUserRating = gameDetailsFromApi?.Rating ?? 0,
                IgdbCriticRating = gameDetailsFromApi?.Aggregated_rating ?? 0,
                LocalAverageRating = localAvg,
                LocalRatingCount = localCount,
                UserPersonalRating = personalRating
            };
            // -------------------

            // 2. Znajdź Steam AppID (Reszta logiki bez zmian)
            string steamAppId = null;

            if (gameDetailsFromApi != null)
            {
                if (gameDetailsFromApi.External_games != null)
                {
                    var steamExternal = gameDetailsFromApi.External_games.FirstOrDefault(e => e.Category == 1);
                    if (steamExternal != null) steamAppId = steamExternal.Uid;
                }
                if (string.IsNullOrEmpty(steamAppId) && gameDetailsFromApi.Websites != null)
                {
                    var steamWebsite = gameDetailsFromApi.Websites.FirstOrDefault(w => w.Category == 13);
                    if (steamWebsite != null)
                    {
                        var match = Regex.Match(steamWebsite.Url, @"app/(\d+)");
                        if (match.Success) steamAppId = match.Groups[1].Value;
                    }
                }
            }

            if (string.IsNullOrEmpty(steamAppId) && gameDetailsFromApi != null)
            {
                steamAppId = await _steamService.SearchAppIdAsync(gameDetailsFromApi.Name);
            }

            List<AchievementViewModel> finalAchievements = new List<AchievementViewModel>();
            List<string> steamUnlockedIds = new List<string>();

            bool isSteamConnected = !string.IsNullOrEmpty(user?.SteamId);
            bool isSteamGame = !string.IsNullOrEmpty(steamAppId);

            if (isSteamGame)
            {
                var steamSchema = await _steamService.GetSchemaForGameAsync(steamAppId);

                if (steamSchema != null && steamSchema.Any())
                {
                    var localAchievements = await _context.UserAchievements
                        .Where(ua => ua.UserId == currentUserId && ua.IgdbGameId == gameFromDb.IgdbGameId)
                        .ToListAsync();

                    if (isSteamConnected)
                    {
                        var playerAchievements = await _steamService.GetGameAchievementsAsync(user.SteamId, steamAppId);
                        steamUnlockedIds = playerAchievements
                            .Where(pa => pa.Achieved == 1)
                            .Select(pa => pa.ApiName)
                            .ToList();
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
                }
            }

            gameFromDb.LastAccessed = DateTime.Now;
            int progressPercent = gameFromDb.CurrentUserStoryProgressPercent;

            if (finalAchievements.Any())
            {
                int unlockedCount = finalAchievements.Count(a => a.IsUnlocked);
                int calculatedPercent = (int)Math.Round((double)unlockedCount / finalAchievements.Count * 100);
                progressPercent = calculatedPercent;
                gameFromDb.CurrentUserStoryProgressPercent = calculatedPercent;
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
                CurrentUserStoryProgressPercent = progressPercent,
                Notes = gameFromDb.Notes,
                Achievements = finalAchievements,
                IsSteamConnected = isSteamConnected,
                IsSteamGame = isSteamGame,
                SteamUnlockedAchievementIds = steamUnlockedIds,
                ToDoItems = gameFromDb.ToDoItems.ToList(),
                Ratings = ratingsModel // <--- PRZYPISANIE OCEN
            };

            return View(viewModel);
        }

        public IActionResult Create() { return View(); }

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
                var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);
                List<ApiGame> gamesInfo = null;
                if (!string.IsNullOrEmpty(jsonResponse)) gamesInfo = JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse);

                var gameInfo = gamesInfo?.FirstOrDefault();
                bool hasCover = gameInfo?.Cover != null && !string.IsNullOrEmpty(gameInfo.Cover.Url);

                if (!hasCover)
                {
                    if (Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
                    return RedirectToAction("Index", "Games", new { mode = "browse" });
                }
            }
            catch (Exception ex) { Console.WriteLine($"Błąd Create: {ex.Message}"); }

            bool alreadyExists = await _context.GamesInLibraries.AnyAsync(g => g.UserId == currentUserId && g.IgdbGameId == gameInLibrary.IgdbGameId);
            if (!alreadyExists)
            {
                _context.Add(gameInLibrary);
                await _context.SaveChangesAsync();
                if (Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
                return RedirectToAction(nameof(Details), "Library", new { id = gameInLibrary.Id });
            }

            if (Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            var existingGame = await _context.GamesInLibraries.FirstOrDefaultAsync(g => g.UserId == currentUserId && g.IgdbGameId == gameInLibrary.IgdbGameId);
            if (existingGame != null) return RedirectToAction(nameof(Details), "Library", new { id = existingGame.Id });

            return RedirectToAction("Index", "Games", new { mode = "browse" });
        }

        // --- USUNIĘTO METODY EDIT ---

        // --- NOWA METODA: Aktualizacja notatek (AJAX) ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNotes(int id, string notes)
        {
            var currentUserId = GetCurrentUserId();
            var gameInLibrary = await _context.GamesInLibraries.FirstOrDefaultAsync(g => g.Id == id && g.UserId == currentUserId);

            if (gameInLibrary == null) return Json(new { success = false, error = "Nie znaleziono gry." });

            gameInLibrary.Notes = notes;
            _context.Update(gameInLibrary);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var currentUserId = GetCurrentUserId();
            var gameInLibrary = await _context.GamesInLibraries.FirstOrDefaultAsync(m => m.Id == id && m.UserId == currentUserId);
            if (gameInLibrary == null) return NotFound();
            return View(gameInLibrary);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var currentUserId = GetCurrentUserId();
            var gameInLibrary = await _context.GamesInLibraries.FindAsync(id);
            if (gameInLibrary != null && gameInLibrary.UserId == currentUserId)
            {
                _context.GamesInLibraries.Remove(gameInLibrary);
                var achievements = await _context.UserAchievements.Where(ua => ua.UserId == currentUserId && ua.IgdbGameId == gameInLibrary.IgdbGameId).ToListAsync();
                _context.UserAchievements.RemoveRange(achievements);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAchievement(long igdbGameId, string achievementExternalId)
        {
            if (string.IsNullOrEmpty(achievementExternalId)) return Json(new { success = false, error = "Brak ID." });

            var currentUserId = GetCurrentUserId();
            var achievement = await _context.UserAchievements.FirstOrDefaultAsync(ua => ua.UserId == currentUserId && ua.IgdbGameId == igdbGameId && ua.AchievementExternalId == achievementExternalId);

            bool newStatus;
            if (achievement == null)
            {
                var newAchievement = new UserAchievement { UserId = currentUserId, IgdbGameId = igdbGameId, AchievementExternalId = achievementExternalId, IsUnlocked = true };
                _context.UserAchievements.Add(newAchievement);
                newStatus = true;
            }
            else
            {
                achievement.IsUnlocked = !achievement.IsUnlocked;
                newStatus = achievement.IsUnlocked;
            }
            await _context.SaveChangesAsync();

            // Postęp procentowy przeliczymy w widoku przez JS, a w bazie zaktualizuje się przy następnym odświeżeniu Details/Index

            return Json(new { success = true, isUnlocked = newStatus });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearLibrary()
        {
            var currentUserId = GetCurrentUserId();
            var userGames = await _context.GamesInLibraries.Where(g => g.UserId == currentUserId).ToListAsync();
            if (!userGames.Any()) { TempData["ErrorMessage"] = "Biblioteka pusta."; return RedirectToAction(nameof(Index)); }
            var userAchievements = await _context.UserAchievements.Where(ua => ua.UserId == currentUserId).ToListAsync();
            _context.UserAchievements.RemoveRange(userAchievements);
            _context.GamesInLibraries.RemoveRange(userGames);
            await _context.SaveChangesAsync();
            TempData["StatusMessage"] = "Wyczyszczono bibliotekę.";
            return RedirectToAction(nameof(Index));
        }

        // --- OBSŁUGA TO-DO LIST (AJAX) ---

        [HttpPost]
        public async Task<IActionResult> AddToDoItem(int gameId, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return Json(new { success = false });

            var currentUserId = GetCurrentUserId();
            var game = await _context.GamesInLibraries.FirstOrDefaultAsync(g => g.Id == gameId && g.UserId == currentUserId);

            if (game == null) return Json(new { success = false, message = "Gra nie znaleziona." });

            var newItem = new ToDoItem { Content = content, GameInLibraryId = gameId, IsCompleted = false };
            _context.ToDoItems.Add(newItem);
            await _context.SaveChangesAsync();

            return Json(new { success = true, id = newItem.Id, content = newItem.Content });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleToDoItem(int itemId)
        {
            var currentUserId = GetCurrentUserId();
            var item = await _context.ToDoItems
                .Include(t => t.GameInLibrary)
                .FirstOrDefaultAsync(t => t.Id == itemId && t.GameInLibrary.UserId == currentUserId);

            if (item == null) return Json(new { success = false });

            item.IsCompleted = !item.IsCompleted;
            await _context.SaveChangesAsync();

            return Json(new { success = true, isCompleted = item.IsCompleted });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteToDoItem(int itemId)
        {
            var currentUserId = GetCurrentUserId();
            var item = await _context.ToDoItems
                .Include(t => t.GameInLibrary)
                .FirstOrDefaultAsync(t => t.Id == itemId && t.GameInLibrary.UserId == currentUserId);

            if (item == null) return Json(new { success = false });

            _context.ToDoItems.Remove(item);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCompletedToDoItems(int gameId)
        {
            var currentUserId = GetCurrentUserId();
            var itemsToDelete = await _context.ToDoItems
                .Where(t => t.GameInLibraryId == gameId && t.GameInLibrary.UserId == currentUserId && t.IsCompleted)
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
            var currentUserId = GetCurrentUserId();
            // Usuwamy wszystkie elementy z listy dla tej gry
            var items = await _context.ToDoItems
                .Where(t => t.GameInLibraryId == gameId && t.GameInLibrary.UserId == currentUserId)
                .ToListAsync();

            if (items.Any())
            {
                _context.ToDoItems.RemoveRange(items);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProgress(int id, int percent)
        {
            var currentUserId = GetCurrentUserId();
            var gameInLibrary = await _context.GamesInLibraries
                .FirstOrDefaultAsync(g => g.Id == id && g.UserId == currentUserId);

            if (gameInLibrary == null) return Json(new { success = false, error = "Nie znaleziono gry." });

            // Zabezpieczenie zakresu 0-100
            if (percent < 0) percent = 0;
            if (percent > 100) percent = 100;

            gameInLibrary.CurrentUserStoryProgressPercent = percent;
            _context.Update(gameInLibrary);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}