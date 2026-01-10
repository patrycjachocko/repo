using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa_zesp.Models.Users;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using praca_dyplomowa_zesp.Models.API;
using praca_dyplomowa.Data;
using Newtonsoft.Json;

namespace praca_dyplomowa_zesp.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly SteamApiService _steamService;
        private readonly ApplicationDbContext _context;
        private readonly IGDBClient _igdbClient;

        public ProfileController(UserManager<User> userManager,
                                 SignInManager<User> signInManager,
                                 IWebHostEnvironment webHostEnvironment,
                                 SteamApiService steamService,
                                 ApplicationDbContext context,
                                 IGDBClient igdbClient)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _webHostEnvironment = webHostEnvironment;
            _steamService = steamService;
            _context = context;
            _igdbClient = igdbClient;
        }

        private Task<User> GetCurrentUserAsync() => _userManager.GetUserAsync(User);

        // GET: Profile/Index
        public async Task<IActionResult> Index()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                await _signInManager.SignOutAsync();
                return RedirectToAction("Index", "Home");
            }

            bool hasPassword = await _userManager.HasPasswordAsync(user);

            var viewModel = new ProfileViewModel
            {
                UserId = user.Id,
                Username = user.UserName,
                // USUNIĘTO: Description = user.Description,
                SteamId = user.SteamId,
                IsCreatedBySteam = !hasPassword
            };

            ViewData["StatusMessage"] = TempData["StatusMessage"];
            ViewData["ErrorMessage"] = TempData["ErrorMessage"];

            return View(viewModel);
        }

        // --- SEKCJA STEAM ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LinkSteam()
        {
            var redirectUrl = Url.Action("LinkSteamCallback", "Profile");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Steam", redirectUrl);
            return Challenge(properties, "Steam");
        }

        [HttpGet]
        public async Task<IActionResult> LinkSteamCallback(string remoteError = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                TempData["ErrorMessage"] = "Błąd autoryzacji Steam.";
                return RedirectToAction(nameof(Index));
            }

            var steamIdClaim = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var steamId = steamIdClaim?.Split('/').LastOrDefault();

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.SteamId == steamId);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                TempData["ErrorMessage"] = "To konto Steam jest już połączone z innym użytkownikiem!";
                return RedirectToAction(nameof(Index));
            }

            user.SteamId = steamId;
            await _userManager.UpdateAsync(user);

            TempData["StatusMessage"] = "Pomyślnie połączono konto Steam!";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> SteamLibrary()
        {
            var user = await GetCurrentUserAsync();
            if (user == null || string.IsNullOrEmpty(user.SteamId))
            {
                return RedirectToAction("Index");
            }

            var games = await _steamService.GetUserGamesAsync(user.SteamId);
            return View(games.OrderByDescending(g => g.PlaytimeForever).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportSteamGames()
        {
            var user = await GetCurrentUserAsync();
            if (user == null || string.IsNullOrEmpty(user.SteamId))
            {
                TempData["ErrorMessage"] = "Musisz połączyć konto Steam, aby zaimportować gry.";
                return RedirectToAction("SteamLibrary");
            }

            var steamGames = await _steamService.GetUserGamesAsync(user.SteamId);
            if (!steamGames.Any())
            {
                TempData["ErrorMessage"] = "Nie znaleziono gier na Twoim koncie Steam.";
                return RedirectToAction("SteamLibrary");
            }

            var ownedIgdbIds = await _context.GamesInLibraries
                .Where(g => g.UserId == user.Id)
                .Select(g => g.IgdbGameId)
                .ToHashSetAsync();

            var newGamesMap = new Dictionary<long, int>();
            var notFoundNames = new List<string>();
            var gamesMap = new Dictionary<string, SteamGameDto>();

            foreach (var g in steamGames)
            {
                if (string.IsNullOrWhiteSpace(g.Name)) continue;
                var cleanName = CleanSteamGameName(g.Name);
                if (!string.IsNullOrEmpty(cleanName) && !gamesMap.ContainsKey(cleanName))
                {
                    gamesMap[cleanName] = g;
                }
            }

            // ETAP 1
            var namesToProcess = gamesMap.Keys.ToList();
            int batchSize = 50;

            for (int i = 0; i < namesToProcess.Count; i += batchSize)
            {
                var batchNames = namesToProcess.Skip(i).Take(batchSize).ToList();
                var namesQueryString = string.Join(",", batchNames.Select(n => $"\"{n}\""));
                var query = $"fields id, name, category, version_parent, aggregated_rating; where name = ({namesQueryString}) & category = 0 & version_parent = null; limit 50;";

                try
                {
                    if (i > 0) await Task.Delay(150);
                    var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);
                    if (!string.IsNullOrEmpty(jsonResponse))
                    {
                        var batchResults = JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse);
                        if (batchResults != null && batchResults.Any())
                        {
                            var gamesGroupedByName = batchResults.GroupBy(g => g.Name, StringComparer.OrdinalIgnoreCase);
                            foreach (var group in gamesGroupedByName)
                            {
                                var bestCandidate = group.FirstOrDefault(g => g.Aggregated_rating != null) ?? group.FirstOrDefault();
                                if (bestCandidate != null)
                                {
                                    if (!ownedIgdbIds.Contains(bestCandidate.Id) && !newGamesMap.ContainsKey(bestCandidate.Id))
                                    {
                                        var matchedKey = gamesMap.Keys.FirstOrDefault(k => k.Equals(bestCandidate.Name, StringComparison.OrdinalIgnoreCase));
                                        if (matchedKey != null)
                                        {
                                            newGamesMap.Add(bestCandidate.Id, gamesMap[matchedKey].AppId);
                                            gamesMap.Remove(matchedKey);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine($"Błąd w batchu: {ex.Message}"); }
            }

            // ETAP 2
            var remainingGames = gamesMap.Values.ToList();
            foreach (var steamGame in remainingGames)
            {
                var cleanName = CleanSteamGameName(steamGame.Name);
                var query = $"fields id, name, category, version_parent, aggregated_rating; search \"{cleanName}\"; where parent_game = null; limit 10;";
                bool gameFound = false;

                try
                {
                    await Task.Delay(250);
                    var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);
                    if (!string.IsNullOrEmpty(jsonResponse))
                    {
                        var gamesFromApi = JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse);
                        if (gamesFromApi != null && gamesFromApi.Any())
                        {
                            var validCandidates = gamesFromApi.Where(g =>
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
                                var bestMatch = validCandidates.FirstOrDefault(g => g.Aggregated_rating != null) ?? validCandidates.FirstOrDefault();
                                if (bestMatch != null)
                                {
                                    gameFound = true;
                                    if (!ownedIgdbIds.Contains(bestMatch.Id) && !newGamesMap.ContainsKey(bestMatch.Id))
                                    {
                                        newGamesMap.Add(bestMatch.Id, steamGame.AppId);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine($"Błąd fallback: {ex.Message}"); }

                if (!gameFound) notFoundNames.Add(steamGame.Name);
            }

            // Zapis
            if (newGamesMap.Any())
            {
                var newLibraryEntries = new List<GameInLibrary>();
                foreach (var item in newGamesMap)
                {
                    long igdbId = item.Key;
                    int steamAppId = item.Value;
                    int calculatedPercent = 0;

                    try
                    {
                        var achievements = await _steamService.GetGameAchievementsAsync(user.SteamId, steamAppId.ToString());
                        if (achievements != null && achievements.Any())
                        {
                            int total = achievements.Count;
                            int unlocked = achievements.Count(a => a.Achieved == 1);
                            if (total > 0) calculatedPercent = (int)Math.Round((double)unlocked / total * 100);
                        }
                    }
                    catch { }

                    newLibraryEntries.Add(new GameInLibrary
                    {
                        UserId = user.Id,
                        IgdbGameId = igdbId,
                        DateAddedToLibrary = DateTime.Now,
                        CurrentUserStoryProgressPercent = calculatedPercent
                    });
                }

                await _context.GamesInLibraries.AddRangeAsync(newLibraryEntries);
                await _context.SaveChangesAsync();
                TempData["StatusMessage"] = $"Sukces! Dodano {newLibraryEntries.Count} nowych gier. Postęp został zaktualizowany ze Steam.";
            }
            else
            {
                TempData["StatusMessage"] = "Przetworzono bibliotekę. Nie znaleziono nowych gier do dodania.";
            }

            if (notFoundNames.Any())
            {
                int maxDisplay = 10;
                var namesToShow = notFoundNames.Take(maxDisplay);
                string missingList = string.Join(", ", namesToShow);
                string errorMsg = $"Nie udało się znaleźć w bazie IGDB {notFoundNames.Count} gier: {missingList}";
                if (notFoundNames.Count > maxDisplay) errorMsg += $" ...i {notFoundNames.Count - maxDisplay} innych.";
                TempData["ErrorMessage"] = errorMsg;
            }

            return RedirectToAction("SteamLibrary");
        }

        private string CleanSteamGameName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return name.Replace("\"", "").Replace("™", "").Replace("®", "").Replace("©", "").Trim();
        }

        private class IgdbExternalGameDto
        {
            [JsonProperty("game")]
            public long Game { get; set; }
            [JsonProperty("uid")]
            public string Uid { get; set; }
            [JsonProperty("category")]
            public int Category { get; set; }
        }

        // GET: Profile/GetAvatar/GUID
        [AllowAnonymous]
        public async Task<IActionResult> GetAvatar(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user != null && user.ProfilePicture != null && !string.IsNullOrEmpty(user.ProfilePictureContentType))
            {
                return File(user.ProfilePicture, user.ProfilePictureContentType);
            }

            var defaultAvatarPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "avatars", "default_avatar.png");
            if (!System.IO.File.Exists(defaultAvatarPath)) return NotFound();
            return PhysicalFile(defaultAvatarPath, "image/png");
        }

        // USUNIĘTO: GetBanner

        // POST: /Profile/ChangeAvatar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeAvatar(IFormFile avatarFile)
        {
            if (avatarFile == null || avatarFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Nie wybrano pliku.";
                return RedirectToAction(nameof(Index));
            }

            var user = await GetCurrentUserAsync();
            if (user == null) return NotFound();

            if (avatarFile.Length > 2 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "Plik jest za duży (maksymalnie 2MB).";
                return RedirectToAction(nameof(Index));
            }
            var allowedTypes = new[] { "image/jpeg", "image/png" };
            if (!allowedTypes.Contains(avatarFile.ContentType))
            {
                TempData["ErrorMessage"] = "Nieprawidłowy format pliku (dozwolone JPG i PNG).";
                return RedirectToAction(nameof(Index));
            }

            using (var memoryStream = new MemoryStream())
            {
                await avatarFile.CopyToAsync(memoryStream);
                user.ProfilePicture = memoryStream.ToArray();
                user.ProfilePictureContentType = avatarFile.ContentType;
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["StatusMessage"] = "Awatar został zaktualizowany.";
            }
            else
            {
                TempData["ErrorMessage"] = "Błąd podczas aktualizacji awatara.";
            }

            return RedirectToAction(nameof(Index));
        }

        // USUNIĘTO: ChangeBanner
        // USUNIĘTO: UpdateDescription

        // POST: /Profile/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword([Bind(Prefix = "changePasswordModel")] ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Wprowadzone dane do zmiany hasła są nieprawidłowe (np. hasła się nie zgadzają).";
                return RedirectToAction(nameof(Index));
            }

            var user = await GetCurrentUserAsync();
            if (user == null) return NotFound();

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                var errors = string.Join(" ", changePasswordResult.Errors.Select(e => e.Description));
                TempData["ErrorMessage"] = $"Błąd zmiany hasła: {errors}";
                return RedirectToAction(nameof(Index));
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["StatusMessage"] = "Hasło zostało zmienione.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetIgdbCover(string steamId, string gameName)
        {
            if (string.IsNullOrEmpty(steamId)) return Json(new { url = "" });

            string coverUrl = null;

            try
            {
                var query = $"fields game.cover.url; where category = 1 & uid = \"{steamId}\"; limit 1;";
                var json = await _igdbClient.ApiRequestAsync("external_games", query);

                if (!string.IsNullOrEmpty(json))
                {
                    var result = JsonConvert.DeserializeObject<List<dynamic>>(json);
                    if (result != null && result.Count > 0)
                    {
                        string url = result[0]?.game?.cover?.url;
                        if (!string.IsNullOrEmpty(url)) coverUrl = url;
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine($"Błąd pobierania okładki po ID: {ex.Message}"); }

            if (string.IsNullOrEmpty(coverUrl) && !string.IsNullOrEmpty(gameName))
            {
                try
                {
                    var cleanName = CleanSteamGameName(gameName);
                    var query = $"fields cover.url; search \"{cleanName}\"; limit 1;";
                    var json = await _igdbClient.ApiRequestAsync("games", query);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var result = JsonConvert.DeserializeObject<List<dynamic>>(json);
                        string url = result?.FirstOrDefault()?.cover?.url;
                        if (!string.IsNullOrEmpty(url)) coverUrl = url;
                    }
                }
                catch (Exception ex) { Console.WriteLine($"Błąd pobierania okładki po nazwie: {ex.Message}"); }
            }

            if (!string.IsNullOrEmpty(coverUrl))
            {
                coverUrl = coverUrl.Replace("t_thumb", "t_cover_big");
                if (coverUrl.StartsWith("//")) coverUrl = "https:" + coverUrl;
            }

            return Json(new { url = coverUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            await _signInManager.SignOutAsync();
            await _userManager.DeleteAsync(user);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisconnectSteam()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            // 1. Pobierz aktualne logowania, aby znaleźć klucz Steam (ProviderKey)
            var logins = await _userManager.GetLoginsAsync(user);
            var steamLogin = logins.FirstOrDefault(x => x.LoginProvider == "Steam");

            IdentityResult result = IdentityResult.Success;

            // Jeśli istnieje powiązanie w tabeli AspNetUserLogins, usuń je
            if (steamLogin != null)
            {
                result = await _userManager.RemoveLoginAsync(user, "Steam", steamLogin.ProviderKey);
            }

            if (result.Succeeded || steamLogin == null)
            {
                // 2. Usuń SteamId z tabeli Users
                user.SteamId = null;
                await _userManager.UpdateAsync(user);

                // 3. AKTUALIZACJA POSTĘPU GIER W BIBLIOTECE
                // Ponieważ odłączyliśmy Steam, musimy przeliczyć procenty dla każdej gry
                // na podstawie tego, co zostało w lokalnej bazie (UserAchievements).

                var userGames = await _context.GamesInLibraries
                    .Where(g => g.UserId == user.Id)
                    .ToListAsync();

                if (userGames.Any())
                {
                    foreach (var game in userGames)
                    {
                        // Sprawdzamy ile osiągnięć jest w lokalnej bazie dla tej gry
                        var localStats = await _context.UserAchievements
                            .Where(ua => ua.UserId == user.Id && ua.IgdbGameId == game.IgdbGameId)
                            .Select(ua => new { ua.IsUnlocked }) // Pobieramy tylko status
                            .ToListAsync();

                        int total = localStats.Count;
                        int unlocked = localStats.Count(x => x.IsUnlocked);

                        if (total > 0)
                        {
                            // Przeliczamy na podstawie lokalnych danych
                            game.CurrentUserStoryProgressPercent = (int)Math.Round((double)unlocked / total * 100);
                        }
                        else
                        {
                            // Jeśli w bazie nie ma żadnych osiągnięć (bo były czytane "na żywo" ze Steam),
                            // to po odłączeniu postęp spada do 0.
                            game.CurrentUserStoryProgressPercent = 0;
                        }
                    }

                    // Zapisujemy zaktualizowane procenty do bazy
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Konto Steam zostało odłączone. Statystyki gier zostały zaktualizowane.";
            }
            else
            {
                TempData["Error"] = "Wystąpił błąd podczas usuwania powiązania Steam.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}