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

// Definicje klas API (bez zmian)
namespace praca_dyplomowa_zesp.Models.API
{
    public class ApiGame { public long Id { get; set; } public string Name { get; set; } public ApiCover Cover { get; set; } public List<ApiGenre> Genres { get; set; } public List<ApiInvolvedCompany> Involved_companies { get; set; } public List<ApiReleaseDate> Release_dates { get; set; } }
    public class ApiCover { public string Url { get; set; } }
    public class ApiGenre { public string Name { get; set; } }
    public class ApiInvolvedCompany { public ApiCompany Company { get; set; } public bool developer { get; set; } }
    public class ApiCompany { public string Name { get; set; } }
    public class ApiReleaseDate { public string Human { get; set; } }
    public class ApiAchievement { public long Id { get; set; } public string Name { get; set; } public string Description { get; set; } public ApiAchievementIcon Achievement_icon { get; set; } }
    public class ApiAchievementIcon { public string Url { get; set; } }
}

namespace praca_dyplomowa_zesp.Controllers
{
    [Authorize]
    public class LibraryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IGDBClient _igdbClient;
        private readonly UserManager<User> _userManager;

        public LibraryController(ApplicationDbContext context, IGDBClient igdbClient, UserManager<User> userManager)
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

        // GET: Library
        // Zastąp starą metodę Index tą poniżej:
        public async Task<IActionResult> Index()
        {
            var currentUserId = GetCurrentUserId();

            // 1. Pobieramy gry użytkownika z lokalnej bazy
            var userGamesFromDb = await _context.GamesInLibraries
                .Where(g => g.UserId == currentUserId)
                .OrderByDescending(g => g.DateAddedToLibrary) // Opcjonalnie: sortowanie od najnowszych
                .ToListAsync();

            if (!userGamesFromDb.Any())
            {
                return View(new List<MainLibraryViewModel>());
            }

            // 2. Przygotowujemy listę na dane z API
            var allApiGames = new List<ApiGame>();

            // Pobieramy same ID
            var allIgdbIds = userGamesFromDb.Select(g => g.IgdbGameId).Distinct().ToList();

            // 3. Pobieramy dane z IGDB partiami (np. po 50 sztuk), aby ominąć limity
            int batchSize = 50;
            for (int i = 0; i < allIgdbIds.Count; i += batchSize)
            {
                var batchIds = allIgdbIds.Skip(i).Take(batchSize).ToList();
                var idsString = string.Join(",", batchIds);

                // WAŻNE: Dodajemy "limit {batchSize}", bo domyślnie IGDB zwraca tylko 10 wyników!
                var query = $"fields name, cover.url; where id = ({idsString}); limit {batchSize};";

                try
                {
                    var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);
                    if (!string.IsNullOrEmpty(jsonResponse))
                    {
                        var chunkGames = JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse);
                        if (chunkGames != null)
                        {
                            allApiGames.AddRange(chunkGames);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd pobierania partii gier w bibliotece: {ex.Message}");
                }
            }

            // 4. Mapujemy wyniki (łączymy dane z Bazy z danymi z API)
            var viewModels = userGamesFromDb.Select(dbGame =>
            {
                // Szukamy odpowiednika w pobranych danych z API
                var apiGame = allApiGames.FirstOrDefault(apiG => apiG.Id == dbGame.IgdbGameId);

                return new MainLibraryViewModel
                {
                    DbId = dbGame.Id,
                    IgdbGameId = dbGame.IgdbGameId,
                    // Jeśli apiGame jest null (co nie powinno się zdarzyć), dajemy fallback
                    Name = apiGame?.Name ?? "Wczytywanie...",
                    CoverUrl = apiGame?.Cover?.Url?.Replace("t_thumb", "t_cover_big") ?? "https://via.placeholder.com/264x352.png?text=Brak+okładki"
                };
            }).ToList();

            return View(viewModels);
        }

        // GET: Library/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            var gameFromDb = await _context.GamesInLibraries.FirstOrDefaultAsync(m => m.Id == id && m.UserId == currentUserId);
            if (gameFromDb == null) return NotFound();

            var gameQuery = $"fields name, cover.url, genres.name, involved_companies.company.name, involved_companies.developer, release_dates.human; where id = {gameFromDb.IgdbGameId}; limit 1;";
            var gameJsonResponse = await _igdbClient.ApiRequestAsync("games", gameQuery);
            var gameDetailsFromApi = (JsonConvert.DeserializeObject<List<ApiGame>>(gameJsonResponse) ?? new List<ApiGame>()).FirstOrDefault();

            var achievementQuery = $"fields name, description, achievement_icon.url; where game = {gameFromDb.IgdbGameId}; limit 50;";
            var achievementJsonResponse = await _igdbClient.ApiRequestAsync("achievements", achievementQuery);

            var achievementsFromApi = new List<ApiAchievement>();
            if (!string.IsNullOrEmpty(achievementJsonResponse))
            {
                achievementsFromApi = JsonConvert.DeserializeObject<List<ApiAchievement>>(achievementJsonResponse) ?? new List<ApiAchievement>();
            }

            var userAchievementsFromDb = await _context.UserAchievements
                .Where(ua => ua.UserId == currentUserId && ua.IgdbGameId == gameFromDb.IgdbGameId)
                .ToListAsync();

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
                Achievements = achievementsFromApi.Select(apiAch => new AchievementViewModel
                {
                    Name = apiAch.Name,
                    Description = apiAch.Description,
                    IconUrl = apiAch.Achievement_icon?.Url?.Replace("t_thumb", "t_screenshot_med") ?? "https://via.placeholder.com/64.png?text=Icon",
                    ExternalId = apiAch.Id.ToString(),
                    IsUnlocked = userAchievementsFromDb.Any(dbAch => dbAch.AchievementExternalId == apiAch.Id.ToString() && dbAch.IsUnlocked)
                }).ToList()
            };

            return View(viewModel);
        }

        // GET: Library/Create
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // ***** POCZĄTEK ZMIANY *****
        // Dodajemy parametr 'string returnUrl'
        public async Task<IActionResult> Create([Bind("IgdbGameId")] GameInLibrary gameInLibrary, string returnUrl)
        // ***** KONIEC ZMIANY *****
        {
            var currentUserId = GetCurrentUserId();
            gameInLibrary.UserId = currentUserId;
            gameInLibrary.DateAddedToLibrary = DateTime.Now;

            bool alreadyExists = await _context.GamesInLibraries.AnyAsync(g => g.UserId == currentUserId && g.IgdbGameId == gameInLibrary.IgdbGameId);
            if (!alreadyExists)
            {
                _context.Add(gameInLibrary);
                await _context.SaveChangesAsync();

                // ***** POCZĄTEK ZMIANY *****
                // Sprawdzamy, czy returnUrl został podany i jest bezpieczny (lokalny)
                if (Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl); // Wracamy na stronę, z której przyszliśmy (np. Guides/Index)
                }

                // Domyślne przekierowanie (używane przez Games/Details)
                return RedirectToAction(nameof(Details), "Library", new { id = gameInLibrary.Id });
                // ***** KONIEC ZMIANY *****
            }

            ModelState.AddModelError("IgdbGameId", "Ta gra jest już w Twojej bibliotece.");

            // ***** POCZĄTEK ZMIANY *****
            // Jeśli gra już istnieje, również obsłuż returnUrl
            if (Url.IsLocalUrl(returnUrl))
            {
                // Użytkownik jest np. na Guides/Index, niech tam zostanie
                return Redirect(returnUrl);
            }

            // Domyślne przekierowanie dla błędu (gdy nie ma returnUrl)
            var existingGame = await _context.GamesInLibraries.FirstOrDefaultAsync(g => g.UserId == currentUserId && g.IgdbGameId == gameInLibrary.IgdbGameId);
            if (existingGame != null)
            {
                return RedirectToAction(nameof(Details), "Library", new { id = existingGame.Id });
            }
            // ***** KONIEC ZMIANY *****

            return RedirectToAction("Index", "Games", new { mode = "browse" });
        }

        // GET: Library/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var currentUserId = GetCurrentUserId();
            var gameInLibrary = await _context.GamesInLibraries.FirstOrDefaultAsync(g => g.Id == id && g.UserId == currentUserId);
            if (gameInLibrary == null) return NotFound();
            return View(gameInLibrary);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,IgdbGameId,UserId,DateAddedToLibrary,CurrentUserStoryMission,CurrentUserStoryProgressPercent,Notes")] GameInLibrary gameInLibrary)
        {
            if (id != gameInLibrary.Id) return NotFound();

            var currentUserId = GetCurrentUserId();
            if (gameInLibrary.UserId != currentUserId) return Forbid();

            _context.Update(gameInLibrary);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = id });
        }

        // GET: Library/Delete/5
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
            if (string.IsNullOrEmpty(achievementExternalId))
            {
                return Json(new { success = false, error = "Brak ID osiągnięcia." });
            }

            var currentUserId = GetCurrentUserId();
            var achievement = await _context.UserAchievements
                .FirstOrDefaultAsync(ua => ua.UserId == currentUserId && ua.IgdbGameId == igdbGameId && ua.AchievementExternalId == achievementExternalId);

            bool newStatus;
            if (achievement == null)
            {
                var newAchievement = new UserAchievement
                {
                    UserId = currentUserId,
                    IgdbGameId = igdbGameId,
                    AchievementExternalId = achievementExternalId,
                    IsUnlocked = true
                };
                _context.UserAchievements.Add(newAchievement);
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
        // Plik: Controllers/LibraryController.cs

        // --- NOWA METODA: Wyczyść całą bibliotekę ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearLibrary()
        {
            var currentUserId = GetCurrentUserId();

            // 1. Pobierz wszystkie gry użytkownika
            var userGames = await _context.GamesInLibraries
                .Where(g => g.UserId == currentUserId)
                .ToListAsync();

            if (!userGames.Any())
            {
                TempData["ErrorMessage"] = "Twój biblioteka jest już pusta.";
                return RedirectToAction(nameof(Index));
            }

            // 2. Pobierz wszystkie osiągnięcia użytkownika (aby nie zostawiać "sierot" w bazie)
            // Zakładamy, że jeśli usuwasz gry, chcesz też usunąć postęp w nich.
            var userAchievements = await _context.UserAchievements
                .Where(ua => ua.UserId == currentUserId)
                .ToListAsync();

            // 3. Usuń dane masowo
            _context.UserAchievements.RemoveRange(userAchievements);
            _context.GamesInLibraries.RemoveRange(userGames);

            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Wyczyszczono bibliotekę. Wszystkie gry zostały usunięte.";
            return RedirectToAction(nameof(Index));
        }
    }
}