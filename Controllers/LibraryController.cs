using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using praca_dyplomowa_zesp.Models.API;
using Microsoft.AspNetCore.Identity;      // <-- DODANE
using praca_dyplomowa_zesp.Models.Users; // <-- DODANE
using Microsoft.AspNetCore.Authorization;  // <-- DODANE

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
    [Authorize] // <-- DODANE: Wymusza logowanie dla wszystkich akcji w tym kontrolerze
    public class LibraryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IGDBClient _igdbClient;
        private readonly UserManager<User> _userManager; // <-- DODANE: Do zarządzania użytkownikiem

        // private static readonly Guid TEST_USER_ID = ...; // <-- USUNIĘTE

        public LibraryController(ApplicationDbContext context, IGDBClient igdbClient, UserManager<User> userManager)
        {
            _context = context;
            _igdbClient = igdbClient;
            _userManager = userManager; // <-- DODANE
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
            return Guid.Empty; // Sytuacja awaryjna, nie powinna wystąpić przy [Authorize]
        }

        // GET: Library
        public async Task<IActionResult> Index()
        {
            var currentUserId = GetCurrentUserId(); // <-- ZMIANA
            var userGamesFromDb = await _context.GamesInLibraries
                .Where(g => g.UserId == currentUserId) // <-- ZMIANA
                .ToListAsync();

            if (!userGamesFromDb.Any())
            {
                return View(new List<MainLibraryViewModel>());
            }

            var igdbGameIds = userGamesFromDb.Select(g => g.IgdbGameId).ToArray();
            var query = $"fields name, cover.url; where id = ({string.Join(",", igdbGameIds)});";
            var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);

            var gamesFromApi = string.IsNullOrEmpty(jsonResponse)
                ? new List<ApiGame>()
                : JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse) ?? new List<ApiGame>();

            var viewModels = userGamesFromDb.Select(dbGame =>
            {
                var apiGame = gamesFromApi.FirstOrDefault(apiG => apiG.Id == dbGame.IgdbGameId);
                return new MainLibraryViewModel
                {
                    DbId = dbGame.Id,
                    IgdbGameId = dbGame.IgdbGameId,
                    Name = apiGame?.Name ?? "Nieznana gra",
                    CoverUrl = apiGame?.Cover?.Url?.Replace("t_thumb", "t_cover_big") ?? "https://via.placeholder.com/264x352.png?text=Brak+okładki"
                };
            }).ToList();

            return View(viewModels);
        }

        // GET: Library/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var currentUserId = GetCurrentUserId(); // <-- ZMIANA
            var gameFromDb = await _context.GamesInLibraries.FirstOrDefaultAsync(m => m.Id == id && m.UserId == currentUserId); // <-- ZMIANA
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
                .Where(ua => ua.UserId == currentUserId && ua.IgdbGameId == gameFromDb.IgdbGameId) // <-- ZMIANA
                .ToListAsync();

            var viewModel = new GameInLibraryViewModel
            {
                DbId = gameFromDb.Id,
                IgdbGameId = gameFromDb.IgdbGameId,
                Name = gameDetailsFromApi?.Name ?? "Brak nazwy",
                CoverUrl = gameDetailsFromApi?.Cover?.Url?.Replace("t_thumb", "t_cover_big"),
                Genres = gameDetailsFromApi?.Genres?.Select(g => g.Name).ToList(),
                Developer = gameDetailsFromApi?.Involved_companies?.FirstOrDefault(ic => ic.developer)?.Company?.Name,
                ReleaseDate = gameDetailsFromApi?.Release_dates?.FirstOrDefault()?.Human,
                DateAddedToLibrary = gameFromDb.DateAddedToLibrary,
                CurrentUserStoryMission = gameFromDb.CurrentUserStoryMission,
                CurrentUserStoryProgressPercent = gameFromDb.CurrentUserStoryProgressPercent,
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
        public async Task<IActionResult> Create([Bind("IgdbGameId")] GameInLibrary gameInLibrary)
        {
            var currentUserId = GetCurrentUserId(); // <-- ZMIANA
            gameInLibrary.UserId = currentUserId; // <-- ZMIANA (GŁÓWNA POPRAWKA BŁĘDU)
            gameInLibrary.DateAddedToLibrary = DateTime.Now;

            bool alreadyExists = await _context.GamesInLibraries.AnyAsync(g => g.UserId == currentUserId && g.IgdbGameId == gameInLibrary.IgdbGameId); // <-- ZMIANA
            if (!alreadyExists)
            {
                _context.Add(gameInLibrary);
                await _context.SaveChangesAsync(); // Ten błąd już nie wystąpi
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("IgdbGameId", "Ta gra jest już w Twojej bibliotece.");
            return View(gameInLibrary);
        }

        // GET: Library/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var currentUserId = GetCurrentUserId(); // <-- ZMIANA
            var gameInLibrary = await _context.GamesInLibraries.FirstOrDefaultAsync(g => g.Id == id && g.UserId == currentUserId); // <-- ZMIANA
            if (gameInLibrary == null) return NotFound();
            return View(gameInLibrary);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,IgdbGameId,UserId,DateAddedToLibrary,CurrentUserStoryMission,CurrentUserStoryProgressPercent")] GameInLibrary gameInLibrary)
        {
            if (id != gameInLibrary.Id) return NotFound();

            var currentUserId = GetCurrentUserId(); // <-- ZMIANA
            if (gameInLibrary.UserId != currentUserId) return Forbid(); // <-- ZMIANA (Zabezpieczenie)

            _context.Update(gameInLibrary);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Library/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var currentUserId = GetCurrentUserId(); // <-- ZMIANA
            var gameInLibrary = await _context.GamesInLibraries.FirstOrDefaultAsync(m => m.Id == id && m.UserId == currentUserId); // <-- ZMIANA
            if (gameInLibrary == null) return NotFound();
            return View(gameInLibrary);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var currentUserId = GetCurrentUserId(); // <-- ZMIANA
            var gameInLibrary = await _context.GamesInLibraries.FindAsync(id);
            if (gameInLibrary != null && gameInLibrary.UserId == currentUserId) // <-- ZMIANA
            {
                _context.GamesInLibraries.Remove(gameInLibrary);
                var achievements = await _context.UserAchievements.Where(ua => ua.UserId == currentUserId && ua.IgdbGameId == gameInLibrary.IgdbGameId).ToListAsync(); // <-- ZMIANA
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

            var currentUserId = GetCurrentUserId(); // <-- ZMIANA
            var achievement = await _context.UserAchievements
                .FirstOrDefaultAsync(ua => ua.UserId == currentUserId && ua.IgdbGameId == igdbGameId && ua.AchievementExternalId == achievementExternalId); // <-- ZMIANA

            bool newStatus;
            if (achievement == null)
            {
                var newAchievement = new UserAchievement
                {
                    UserId = currentUserId, // <-- ZMIANA
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
    }
}