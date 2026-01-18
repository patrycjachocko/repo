using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using praca_dyplomowa_zesp.Models.API;
using praca_dyplomowa_zesp.Services;
using praca_dyplomowa_zesp.Data;
using praca_dyplomowa_zesp.Models.ViewModels.Users;
using praca_dyplomowa_zesp.Models.Modules.Users;
using praca_dyplomowa_zesp.Models.Modules.Libraries;

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

        public ProfileController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IWebHostEnvironment webHostEnvironment,
            SteamApiService steamService,
            ApplicationDbContext context,
            IGDBClient igdbClient)
        {
            //przypisanie wstrzyknietych serwisow do pol klasy
            _userManager = userManager;
            _signInManager = signInManager;
            _webHostEnvironment = webHostEnvironment;
            _steamService = steamService;
            _context = context;
            _igdbClient = igdbClient;
        }

        #region Profile View & Basic Actions

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                await _signInManager.SignOutAsync();
                return RedirectToAction("Index", "Home");
            }

            //sprawdzenie czy konto ma ustawione haslo w celu identyfikacji kont steam
            bool hasPassword = await _userManager.HasPasswordAsync(user);

            var viewModel = new ProfileViewModel
            {
                UserId = user.Id,
                Username = user.UserName,
                SteamId = user.SteamId,
                IsCreatedBySteam = !hasPassword
            };

            ViewData["StatusMessage"] = TempData["StatusMessage"];
            ViewData["ErrorMessage"] = TempData["ErrorMessage"];

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            //calkowite usuniecie uzytkownika poprzedzone zamknieciem sesji
            await _signInManager.SignOutAsync();
            await _userManager.DeleteAsync(user);

            return RedirectToAction("Index", "Home");
        }

        #endregion

        #region Password & Avatar Management

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword([Bind(Prefix = "changePasswordModel")] ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Wprowadzone dane są nieprawidłowe.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            //bezpieczna zmiana hasla przy uzyciu wbudowanej metody identity
            var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = $"Błąd: {string.Join(" ", result.Errors.Select(e => e.Description))}";
                return RedirectToAction(nameof(Index));
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["StatusMessage"] = "Hasło zostało zmienione.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeAvatar(IFormFile avatarFile)
        {
            if (avatarFile == null || avatarFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Nie wybrano pliku.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            //walidacja limitu wielkosci pliku graficznego
            if (avatarFile.Length > 2 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "Plik przekracza limit 2MB.";
                return RedirectToAction(nameof(Index));
            }

            var allowedTypes = new[] { "image/jpeg", "image/png" };
            if (!allowedTypes.Contains(avatarFile.ContentType))
            {
                TempData["ErrorMessage"] = "Dozwolone formaty to JPG i PNG.";
                return RedirectToAction(nameof(Index));
            }

            using (var memoryStream = new MemoryStream())
            {
                //konwersja pliku na format binarny do zapisu w kolumnie bazy danych
                await avatarFile.CopyToAsync(memoryStream);
                user.ProfilePicture = memoryStream.ToArray();
                user.ProfilePictureContentType = avatarFile.ContentType;
            }

            var result = await _userManager.UpdateAsync(user);
            TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] =
                result.Succeeded ? "Awatar został zaktualizowany." : "Błąd podczas zapisu awatara.";

            return RedirectToAction(nameof(Index));
        }

        [AllowAnonymous]
        public async Task<IActionResult> GetAvatar(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user?.ProfilePicture != null && !string.IsNullOrEmpty(user.ProfilePictureContentType))
            {
                //zwrocenie pliku binarnego zapisanego w profilu uzytkownika
                return File(user.ProfilePicture, user.ProfilePictureContentType);
            }

            //wybor domyslnego pliku z dysku jesli uzytkownik nie posiada wlasnego zdjecia
            string fileName = "default_avatar.png";
            if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
            {
                fileName = "admin_avatar.png";
            }

            var path = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "avatars", fileName);

            if (!System.IO.File.Exists(path)) return NotFound();
            return PhysicalFile(path, "image/png");
        }

        #endregion

        #region Steam Integration

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult LinkSteam()
        {
            //przygotowanie przekierowania do zewnetrznej autoryzacji steam
            var redirectUrl = Url.Action(nameof(LinkSteamCallback), "Profile");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Steam", redirectUrl);
            return Challenge(properties, "Steam");
        }

        [HttpGet]
        public async Task<IActionResult> LinkSteamCallback()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                TempData["ErrorMessage"] = "Błąd autoryzacji Steam.";
                return RedirectToAction(nameof(Index));
            }

            //ekstrakcja identyfikatora steamid z roszczen autoryzacyjnych
            var steamId = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value?.Split('/').LastOrDefault();
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.SteamId == steamId);

            //blokada podlaczenia konta steam ktore jest juz przypisane do innego profilu
            if (existingUser != null && existingUser.Id != user.Id)
            {
                TempData["ErrorMessage"] = "To konto Steam jest już przypisane do innego użytkownika!";
                return RedirectToAction(nameof(Index));
            }

            user.SteamId = steamId;
            await _userManager.UpdateAsync(user);

            TempData["StatusMessage"] = "Pomyślnie połączono konto Steam!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DisconnectSteam()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var logins = await _userManager.GetLoginsAsync(user);
            var steamLogin = logins.FirstOrDefault(x => x.LoginProvider == "Steam");

            if (steamLogin != null)
            {
                //usuniecie zewnetrznego powiazania logowania z bazy identity
                await _userManager.RemoveLoginAsync(user, "Steam", steamLogin.ProviderKey);
            }

            user.SteamId = null;
            await _userManager.UpdateAsync(user);

            var userGames = await _context.GamesInLibraries.Where(g => g.UserId == user.Id).ToListAsync();
            foreach (var game in userGames)
            {
                //recalc: przeliczenie postepu gry na podstawie osiagniec zapisanych lokalnie po odlaczeniu steam
                var localStats = await _context.UserAchievements
                    .Where(ua => ua.UserId == user.Id && ua.IgdbGameId == game.IgdbGameId)
                    .Select(ua => ua.IsUnlocked)
                    .ToListAsync();

                game.CurrentUserStoryProgressPercent = localStats.Any()
                    ? (int)Math.Round((double)localStats.Count(x => x) / localStats.Count * 100)
                    : 0;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Konto Steam odłączone. Zaktualizowano statystyki lokalne.";
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Steam Library & Import

        public async Task<IActionResult> SteamLibrary()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || string.IsNullOrEmpty(user.SteamId)) return RedirectToAction(nameof(Index));

            //pobranie pelnej listy gier z api steam i sortowanie po czasie rozgrywki
            var games = await _steamService.GetUserGamesAsync(user.SteamId);
            return View(games.OrderByDescending(g => g.PlaytimeForever).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportSteamGames()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || string.IsNullOrEmpty(user.SteamId))
            {
                TempData["ErrorMessage"] = "Połącz konto Steam przed importem.";
                return RedirectToAction(nameof(SteamLibrary));
            }

            var steamGames = await _steamService.GetUserGamesAsync(user.SteamId);
            if (!steamGames.Any())
            {
                TempData["ErrorMessage"] = "Nie znaleziono gier na Steam.";
                return RedirectToAction(nameof(SteamLibrary));
            }

            var ownedIgdbIds = await _context.GamesInLibraries.Where(g => g.UserId == user.Id).Select(g => g.IgdbGameId).ToHashSetAsync();
            var newGamesMap = new Dictionary<long, int>();
            var notFoundNames = new List<string>();
            var gamesMap = steamGames.Where(g => !string.IsNullOrWhiteSpace(g.Name))
                                     .GroupBy(g => CleanSteamGameName(g.Name))
                                     .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var namesToProcess = gamesMap.Keys.ToList();
            //iteracyjne dopasowywanie nazw gier w paczkach po 50 sztuk do bazy igdb
            for (int i = 0; i < namesToProcess.Count; i += 50)
            {
                var batch = namesToProcess.Skip(i).Take(50).ToList();
                var query = $"fields id, name, category, version_parent, aggregated_rating; where name = ({string.Join(",", batch.Select(n => $"\"{n}\""))}) & category = 0 & version_parent = null; limit 50;";

                //wprowadzenie opoznienia w celu unikniecia limitow zapytan do api
                if (i > 0) await Task.Delay(150);
                var results = await ExecuteIgdbSearch(query);

                foreach (var group in results.GroupBy(g => g.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var best = group.OrderByDescending(g => g.Aggregated_rating).FirstOrDefault();
                    if (best != null && !ownedIgdbIds.Contains(best.Id) && !newGamesMap.ContainsKey(best.Id))
                    {
                        var key = gamesMap.Keys.FirstOrDefault(k => k.Equals(best.Name, StringComparison.OrdinalIgnoreCase));
                        if (key != null)
                        {
                            newGamesMap.Add(best.Id, gamesMap[key].AppId);
                            gamesMap.Remove(key);
                        }
                    }
                }
            }

            foreach (var steamGame in gamesMap.Values.ToList())
            {
                //fallback: proba dopasowania pozostalych gier przy uzyciu wyszukiwania pelnotekstowego
                var query = $"fields id, name, category, version_parent, aggregated_rating; search \"{CleanSteamGameName(steamGame.Name)}\"; where parent_game = null; limit 10;";
                await Task.Delay(250);
                var searchResults = await ExecuteIgdbSearch(query);

                var best = searchResults.Where(g => g.Category == 0 && g.Version_parent == null).OrderByDescending(g => g.Aggregated_rating).FirstOrDefault();
                if (best != null)
                {
                    if (!ownedIgdbIds.Contains(best.Id) && !newGamesMap.ContainsKey(best.Id))
                        newGamesMap.Add(best.Id, steamGame.AppId);
                }
                else
                {
                    notFoundNames.Add(steamGame.Name);
                }
            }

            await SaveImportedGames(user, newGamesMap);
            HandleImportErrors(notFoundNames);

            return RedirectToAction(nameof(SteamLibrary));
        }

        [HttpGet]
        public async Task<IActionResult> GetIgdbCover(string steamId, string gameName)
        {
            if (string.IsNullOrEmpty(steamId)) return Json(new { url = "" });
            string coverUrl = null;

            //wyszukanie okladki w api na podstawie zewnetrznego identyfikatora steam uid
            var json = await _igdbClient.ApiRequestAsync("external_games", $"fields game.cover.url; where category = 1 & uid = \"{steamId}\"; limit 1;");
            if (!string.IsNullOrEmpty(json))
            {
                var result = JsonConvert.DeserializeObject<List<dynamic>>(json);
                coverUrl = result?.FirstOrDefault()?.game?.cover?.url;
            }

            if (string.IsNullOrEmpty(coverUrl) && !string.IsNullOrEmpty(gameName))
            {
                //proba pobrania okladki na podstawie nazwy jesli uid nie zwrocilo wynikow
                var searchJson = await _igdbClient.ApiRequestAsync("games", $"fields cover.url; search \"{CleanSteamGameName(gameName)}\"; limit 1;");
                var searchResult = JsonConvert.DeserializeObject<List<dynamic>>(searchJson);
                coverUrl = searchResult?.FirstOrDefault()?.cover?.url;
            }

            if (!string.IsNullOrEmpty(coverUrl))
            {
                coverUrl = coverUrl.Replace("t_thumb", "t_cover_big");
                if (coverUrl.StartsWith("//")) coverUrl = "https:" + coverUrl;
            }

            return Json(new { url = coverUrl });
        }

        #endregion

        #region Helpers

        private async Task<List<IGDBGameDtos>> ExecuteIgdbSearch(string query)
        {
            try
            {
                var json = await _igdbClient.ApiRequestAsync("games", query);
                return string.IsNullOrEmpty(json) ? new List<IGDBGameDtos>() : JsonConvert.DeserializeObject<List<IGDBGameDtos>>(json);
            }
            catch { return new List<IGDBGameDtos>(); }
        }

        private async Task SaveImportedGames(User user, Dictionary<long, int> gamesToImport)
        {
            if (!gamesToImport.Any()) return;

            var entries = new List<GameInLibrary>();
            foreach (var item in gamesToImport)
            {
                int progress = 0;
                try
                {
                    //pobranie stanu osiagniec dla importowanej gry w celu ustalenia postepu startowego
                    var achievements = await _steamService.GetGameAchievementsAsync(user.SteamId, item.Value.ToString());
                    if (achievements != null && achievements.Any())
                    {
                        progress = (int)Math.Round((double)achievements.Count(a => a.Achieved == 1) / achievements.Count * 100);
                    }
                }
                catch { }

                entries.Add(new GameInLibrary
                {
                    UserId = user.Id,
                    IgdbGameId = item.Key,
                    DateAddedToLibrary = DateTime.Now,
                    CurrentUserStoryProgressPercent = progress
                });
            }

            await _context.GamesInLibraries.AddRangeAsync(entries);
            await _context.SaveChangesAsync();
            TempData["StatusMessage"] = $"Zaimportowano {entries.Count} gier ze Steam.";
        }

        private void HandleImportErrors(List<string> notFoundNames)
        {
            if (!notFoundNames.Any()) return;
            //skrocenie listy bledow importu do pierwszych 10 pozycji w komunikacie
            string list = string.Join(", ", notFoundNames.Take(10));
            TempData["ErrorMessage"] = $"Nie znaleziono {notFoundNames.Count} gier w IGDB: {list} " + (notFoundNames.Count > 10 ? "..." : "");
        }

        private string CleanSteamGameName(string name) =>
            string.IsNullOrEmpty(name) ? "" : name.Replace("\"", "").Replace("™", "").Replace("®", "").Replace("©", "").Trim();

        #endregion
    }
}