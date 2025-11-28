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

// --- Definicje klas API (BEZ ZMIAN) ---
namespace praca_dyplomowa_zesp.Models.API
{
    public class ApiGame
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("cover")] public ApiCover Cover { get; set; }
        [JsonProperty("genres")] public List<ApiGenre> Genres { get; set; }
        [JsonProperty("involved_companies")] public List<ApiInvolvedCompany> Involved_companies { get; set; }
        [JsonProperty("release_dates")] public List<ApiReleaseDate> Release_dates { get; set; }
        [JsonProperty("websites")] public List<ApiWebsite> Websites { get; set; }
        [JsonProperty("external_games")] public List<ApiExternalGame> External_games { get; set; }
    }

    public class ApiCover { [JsonProperty("url")] public string Url { get; set; } }
    public class ApiGenre { [JsonProperty("name")] public string Name { get; set; } }
    public class ApiInvolvedCompany { [JsonProperty("company")] public ApiCompany Company { get; set; } [JsonProperty("developer")] public bool developer { get; set; } }
    public class ApiCompany { [JsonProperty("name")] public string Name { get; set; } }
    public class ApiReleaseDate { [JsonProperty("human")] public string Human { get; set; } }
    public class ApiWebsite { [JsonProperty("category")] public int Category { get; set; } [JsonProperty("url")] public string Url { get; set; } }
    public class ApiExternalGame { [JsonProperty("category")] public int Category { get; set; } [JsonProperty("uid")] public string Uid { get; set; } }
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

        // GET: Library
        public async Task<IActionResult> Index()
        {
            var currentUserId = GetCurrentUserId();
            var userGamesFromDb = await _context.GamesInLibraries
                .Where(g => g.UserId == currentUserId)
                .OrderByDescending(g => g.DateAddedToLibrary)
                .ToListAsync();

            if (!userGamesFromDb.Any()) return View(new List<MainLibraryViewModel>());

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

            var viewModels = userGamesFromDb.Select(dbGame =>
            {
                var apiGame = allApiGames.FirstOrDefault(apiG => apiG.Id == dbGame.IgdbGameId);
                return new MainLibraryViewModel
                {
                    DbId = dbGame.Id,
                    IgdbGameId = dbGame.IgdbGameId,
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
            var user = await _userManager.GetUserAsync(User);

            var gameFromDb = await _context.GamesInLibraries.FirstOrDefaultAsync(m => m.Id == id && m.UserId == currentUserId);
            if (gameFromDb == null) return NotFound();

            // 1. Pobierz dane gry z IGDB
            var gameQuery = $"fields name, cover.url, genres.name, involved_companies.company.name, involved_companies.developer, release_dates.human, websites.url, websites.category, external_games.category, external_games.uid; where id = {gameFromDb.IgdbGameId}; limit 1;";
            var gameJsonResponse = await _igdbClient.ApiRequestAsync("games", gameQuery);

            ApiGame gameDetailsFromApi = null;
            if (!string.IsNullOrEmpty(gameJsonResponse))
            {
                gameDetailsFromApi = (JsonConvert.DeserializeObject<List<ApiGame>>(gameJsonResponse) ?? new List<ApiGame>()).FirstOrDefault();
            }

            // 2. Znajdź Steam AppID
            string steamAppId = null;

            if (gameDetailsFromApi != null)
            {
                // A: External Games
                if (gameDetailsFromApi.External_games != null)
                {
                    var steamExternal = gameDetailsFromApi.External_games.FirstOrDefault(e => e.Category == 1);
                    if (steamExternal != null) steamAppId = steamExternal.Uid;
                }

                // B: Websites
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

            // C: Fallback - Wyszukiwanie po nazwie
            if (string.IsNullOrEmpty(steamAppId) && gameDetailsFromApi != null)
            {
                Console.WriteLine($"[Library] Brak SteamID w IGDB. Szukam po nazwie: {gameDetailsFromApi.Name}");
                steamAppId = await _steamService.SearchAppIdAsync(gameDetailsFromApi.Name);
            }

            List<AchievementViewModel> finalAchievements = new List<AchievementViewModel>();
            List<string> steamUnlockedIds = new List<string>(); // Lista ID osiągnięć zdobytych na Steam

            bool isSteamConnected = !string.IsNullOrEmpty(user?.SteamId);
            bool isSteamGame = !string.IsNullOrEmpty(steamAppId);

            if (isSteamGame)
            {
                var steamSchema = await _steamService.GetSchemaForGameAsync(steamAppId);

                if (steamSchema != null && steamSchema.Any())
                {
                    // ZAWSZE pobieramy postęp lokalny (dla hybrydy)
                    var localAchievements = await _context.UserAchievements
                        .Where(ua => ua.UserId == currentUserId && ua.IgdbGameId == gameFromDb.IgdbGameId)
                        .ToListAsync();

                    if (isSteamConnected)
                    {
                        // Jeśli Steam połączony, pobieramy też postęp ze Steam
                        var playerAchievements = await _steamService.GetGameAchievementsAsync(user.SteamId, steamAppId);

                        // Zapisujemy ID osiągnięć, które są zdobyte na Steamie (status achieved == 1)
                        steamUnlockedIds = playerAchievements
                            .Where(pa => pa.Achieved == 1)
                            .Select(pa => pa.ApiName)
                            .ToList();
                    }

                    // Budujemy listę finałową - SUMA ZBIORÓW
                    finalAchievements = steamSchema.Select(schema => new AchievementViewModel
                    {
                        Name = schema.DisplayName,
                        Description = schema.Description,
                        IconUrl = schema.Icon,
                        ExternalId = schema.ApiName,
                        // Odblokowane jeśli: (Jest na liście Steam) LUB (Jest odblokowane w lokalnej bazie)
                        IsUnlocked = steamUnlockedIds.Contains(schema.ApiName) ||
                                     localAchievements.Any(la => la.AchievementExternalId == schema.ApiName && la.IsUnlocked)
                    }).ToList();
                }
            }

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
                Achievements = finalAchievements,
                IsSteamConnected = isSteamConnected,
                IsSteamGame = isSteamGame,
                SteamUnlockedAchievementIds = steamUnlockedIds // Przekazujemy listę do widoku
            };

            return View(viewModel);
        }

        // GET: Library/Create
        public IActionResult Create() { return View(); }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IgdbGameId")] GameInLibrary gameInLibrary, string returnUrl)
        {
            var currentUserId = GetCurrentUserId();
            gameInLibrary.UserId = currentUserId;
            gameInLibrary.DateAddedToLibrary = DateTime.Now;

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
    }
}