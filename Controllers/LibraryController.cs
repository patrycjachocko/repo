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

        // ZMIANA: Stała na 60
        private const int PageSize = 60;

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Index(string? searchString, string statusFilter = "all", int page = 1)
        {
            var currentUserId = GetCurrentUserId();

            // 1. Pobieramy WSZYSTKIE gry usera z bazy (to jest szybkie, bo to tylko ID i statusy)
            var query = _context.GamesInLibraries
                .Where(g => g.UserId == currentUserId)
                .OrderByDescending(g => g.LastAccessed)
                .AsQueryable();

            // 2. Filtrowanie po statusie (w bazie danych)
            if (statusFilter == "completed") query = query.Where(g => g.CurrentUserStoryProgressPercent == 100);
            else if (statusFilter == "playing") query = query.Where(g => g.CurrentUserStoryProgressPercent > 0 && g.CurrentUserStoryProgressPercent < 100);
            else if (statusFilter == "toplay") query = query.Where(g => g.CurrentUserStoryProgressPercent == 0);

            var filteredDbList = await query.ToListAsync();

            // 3. PAGINACJA
            // Jeśli wyszukujemy po nazwie, musimy niestety pobrać dane dla wszystkich, żeby przefiltrować
            // Jeśli NIE wyszukujemy (domyślny widok), pobieramy API tylko dla 60 gier z obecnej strony!

            List<MainLibraryViewModel> finalGamesList = new List<MainLibraryViewModel>();
            int totalGames = 0;

            if (!string.IsNullOrEmpty(searchString))
            {
                // --- SCENARIUSZ WYSZUKIWANIA (Wolniejszy, ale konieczny do szukania po nazwie) ---
                // Pobieramy dane API dla wszystkich przefiltrowanych statusem gier
                var allIgdbIds = filteredDbList.Select(g => g.IgdbGameId).Distinct().ToList();
                var allApiGames = new List<ApiGame>();

                // Pobieramy w paczkach po 50 (API limit)
                for (int i = 0; i < allIgdbIds.Count; i += 50)
                {
                    var batchIds = allIgdbIds.Skip(i).Take(50).ToList();
                    var queryApi = $"fields name, cover.url; where id = ({string.Join(",", batchIds)}); limit 50;";
                    try
                    {
                        var json = await _igdbClient.ApiRequestAsync("games", queryApi);
                        if (!string.IsNullOrEmpty(json))
                        {
                            var chunk = JsonConvert.DeserializeObject<List<ApiGame>>(json);
                            if (chunk != null) allApiGames.AddRange(chunk);
                        }
                    }
                    catch { }
                }

                // Łączymy i filtrujemy po nazwie
                var allViewModels = filteredDbList.Select(dbGame => {
                    var apiGame = allApiGames.FirstOrDefault(a => a.Id == dbGame.IgdbGameId);
                    return new MainLibraryViewModel
                    {
                        DbId = dbGame.Id,
                        IgdbGameId = dbGame.IgdbGameId,
                        Name = apiGame?.Name ?? "Nieznana",
                        CoverUrl = apiGame?.Cover?.Url?.Replace("t_thumb", "t_cover_big"),
                        ProgressPercent = dbGame.CurrentUserStoryProgressPercent
                    };
                }).Where(g => g.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase)).ToList();

                totalGames = allViewModels.Count;
                finalGamesList = allViewModels.Skip((page - 1) * PageSize).Take(PageSize).ToList();
            }
            else
            {
                // --- SCENARIUSZ OPTYMALNY (Szybkie ładowanie strony) ---
                totalGames = filteredDbList.Count;

                // Bierzemy z bazy TYLKO 60 rekordów dla obecnej strony
                var pagedDbList = filteredDbList.Skip((page - 1) * PageSize).Take(PageSize).ToList();

                // Pobieramy dane z API tylko dla tych 60
                var pageIgdbIds = pagedDbList.Select(g => g.IgdbGameId).Distinct().ToList();
                var pageApiGames = new List<ApiGame>();

                if (pageIgdbIds.Any())
                {
                    var idsString = string.Join(",", pageIgdbIds);
                    // Limit w zapytaniu 100 jest bezpieczny dla 60 ID
                    var queryApi = $"fields name, cover.url; where id = ({idsString}); limit 100;";
                    try
                    {
                        var json = await _igdbClient.ApiRequestAsync("games", queryApi);
                        if (!string.IsNullOrEmpty(json))
                        {
                            pageApiGames = JsonConvert.DeserializeObject<List<ApiGame>>(json) ?? new List<ApiGame>();
                        }
                    }
                    catch { }
                }

                // Mapujemy
                finalGamesList = pagedDbList.Select(dbGame => {
                    var apiGame = pageApiGames.FirstOrDefault(a => a.Id == dbGame.IgdbGameId);
                    return new MainLibraryViewModel
                    {
                        DbId = dbGame.Id,
                        IgdbGameId = dbGame.IgdbGameId,
                        Name = apiGame?.Name ?? "Wczytywanie...",
                        CoverUrl = apiGame?.Cover?.Url?.Replace("t_thumb", "t_cover_big") ?? "https://via.placeholder.com/264x352.png?text=Brak+okładki",
                        ProgressPercent = dbGame.CurrentUserStoryProgressPercent
                    };
                }).ToList();
            }

            var viewModel = new UserLibraryIndexViewModel
            {
                Games = finalGamesList,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(totalGames / (double)PageSize),
                SearchString = searchString,
                StatusFilter = statusFilter
            };

            return View(viewModel);
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
        public async Task<IActionResult> ClearLibrary(string? searchString, string statusFilter = "all")
        {
            var currentUserId = GetCurrentUserId();

            // 1. Pobieramy gry użytkownika (na razie zapytanie, jeszcze nie wykonane w bazie)
            var query = _context.GamesInLibraries
                .Where(g => g.UserId == currentUserId)
                .AsQueryable();

            // 2. Filtrowanie po statusie (W bazie danych)
            if (statusFilter == "completed") query = query.Where(g => g.CurrentUserStoryProgressPercent == 100);
            else if (statusFilter == "playing") query = query.Where(g => g.CurrentUserStoryProgressPercent > 0 && g.CurrentUserStoryProgressPercent < 100);
            else if (statusFilter == "toplay") query = query.Where(g => g.CurrentUserStoryProgressPercent == 0);

            // Lista gier do usunięcia
            List<GameInLibrary> gamesToDelete = new List<GameInLibrary>();

            // 3. Filtrowanie po nazwie (Wymaga API, bo baza nie ma nazw)
            if (!string.IsNullOrEmpty(searchString))
            {
                // Musimy pobrać wstępnie przefiltrowaną listę z bazy, żeby sprawdzić nazwy w API
                var potentialGames = await query.ToListAsync();
                var allIgdbIds = potentialGames.Select(g => g.IgdbGameId).Distinct().ToList();
                var allApiGames = new List<ApiGame>();

                // Pobieramy nazwy z API w paczkach po 50 (tak jak w Index)
                for (int i = 0; i < allIgdbIds.Count; i += 50)
                {
                    var batchIds = allIgdbIds.Skip(i).Take(50).ToList();
                    if (!batchIds.Any()) continue;

                    var queryApi = $"fields name; where id = ({string.Join(",", batchIds)}); limit 50;";
                    try
                    {
                        var json = await _igdbClient.ApiRequestAsync("games", queryApi);
                        if (!string.IsNullOrEmpty(json))
                        {
                            var chunk = JsonConvert.DeserializeObject<List<ApiGame>>(json);
                            if (chunk != null) allApiGames.AddRange(chunk);
                        }
                    }
                    catch { /* Ignoruj błędy API przy usuwaniu */ }
                }

                // Filtrujemy listę gier do usunięcia, sprawdzając czy nazwa z API zawiera szukaną frazę
                // Używamy IGDB ID do powiązania
                gamesToDelete = potentialGames.Where(dbGame =>
                {
                    var apiInfo = allApiGames.FirstOrDefault(a => a.Id == dbGame.IgdbGameId);
                    var gameName = apiInfo?.Name ?? "";
                    return gameName.Contains(searchString, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }
            else
            {
                // Jeśli nie ma wyszukiwania tekstowego, usuwamy wszystko co pasuje do statusu
                gamesToDelete = await query.ToListAsync();
            }

            if (!gamesToDelete.Any())
            {
                TempData["ErrorMessage"] = "Nie znaleziono gier do usunięcia wg podanych kryteriów.";
                return RedirectToAction(nameof(Index), new { searchString, statusFilter });
            }

            // 4. Usuwanie powiązanych osiągnięć
            var gamesIds = gamesToDelete.Select(g => g.IgdbGameId).ToList();
            var userAchievements = await _context.UserAchievements
                .Where(ua => ua.UserId == currentUserId && gamesIds.Contains(ua.IgdbGameId))
                .ToListAsync();

            // 5. Usuwanie powiązanych zadań ToDo
            var dbIds = gamesToDelete.Select(g => g.Id).ToList();
            var todoItems = await _context.ToDoItems
                .Where(t => dbIds.Contains(t.GameInLibraryId))
                .ToListAsync();

            _context.ToDoItems.RemoveRange(todoItems);
            _context.UserAchievements.RemoveRange(userAchievements);
            _context.GamesInLibraries.RemoveRange(gamesToDelete);

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = $"Usunięto {gamesToDelete.Count} gier z biblioteki.";

            // Resetujemy filtry po usunięciu, żeby wrócić do czystego widoku
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