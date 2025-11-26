using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Dodano dla EF Core
using praca_dyplomowa_zesp.Models.Users;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using praca_dyplomowa_zesp.Models.API;
using praca_dyplomowa.Data; // Dodano dla ApplicationDbContext
using Newtonsoft.Json; // Dodano dla deserializacji

namespace praca_dyplomowa_zesp.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly SteamApiService _steamService;
        private readonly ApplicationDbContext _context; // Nowe pole
        private readonly IGDBClient _igdbClient;      // Nowe pole

        public ProfileController(UserManager<User> userManager,
                                 SignInManager<User> signInManager,
                                 IWebHostEnvironment webHostEnvironment,
                                 SteamApiService steamService,
                                 ApplicationDbContext context,  // Wstrzykujemy kontekst bazy
                                 IGDBClient igdbClient)         // Wstrzykujemy klienta IGDB
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

            bool isSteamAccount = user.Login.StartsWith("Steam_");

            var viewModel = new ProfileViewModel
            {
                UserId = user.Id,
                Username = user.UserName,
                Description = user.Description,
                SteamId = user.SteamId,
                IsCreatedBySteam = isSteamAccount
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
            // Tutaj wywołujemy Challenge tak samo jak przy logowaniu, 
            // ale RedirectUrl musi prowadzić do specjalnej akcji "LinkSteamCallback"
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

            // 1. UNIKALNOŚĆ: Sprawdź czy ten SteamID jest już zajęty przez INNEGO użytkownika
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

        [HttpPost]
        public async Task<IActionResult> UnlinkSteam()
        {
            var user = await GetCurrentUserAsync();
            if (user != null)
            {
                user.SteamId = null;
                await _userManager.UpdateAsync(user);
                TempData["StatusMessage"] = "Odłączono konto Steam.";
            }
            return RedirectToAction("Index");
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

            // 1. Pobierz gry ze Steam
            var steamGames = await _steamService.GetUserGamesAsync(user.SteamId);
            if (!steamGames.Any())
            {
                TempData["ErrorMessage"] = "Nie znaleziono gier na Twoim koncie Steam.";
                return RedirectToAction("SteamLibrary");
            }

            // 2. Pobierz listę ID gier, które użytkownik już ma w bibliotece
            var ownedIgdbIds = await _context.GamesInLibraries
                .Where(g => g.UserId == user.Id)
                .Select(g => g.IgdbGameId)
                .ToListAsync();

            // Słownik do przechowywania znalezionych ID gier (klucz: Steam AppId, wartość: IGDB Game Id)
            // Używamy słownika, żeby nie dublować wyników
            var foundGameIds = new HashSet<long>();

            // --- ETAP 1: Szybkie wyszukiwanie po ID (External Games) ---
            // To jest najdokładniejsza metoda, ale czasem brakuje powiązań w bazie IGDB.

            var steamAppIds = steamGames.Select(g => g.AppId.ToString()).Distinct().ToList();
            var mappedSteamIds = new HashSet<string>(); // Tutaj zapiszemy ID steam, które udało się znaleźć w etapie 1

            int batchSize = 50;
            for (int i = 0; i < steamAppIds.Count; i += batchSize)
            {
                var batch = steamAppIds.Skip(i).Take(batchSize).ToList();
                var idsString = string.Join(",", batch.Select(id => $"\"{id}\""));

                // Pobieramy też pole 'uid' żeby wiedzieć, które ID ze Steam zostały znalezione
                var query = $"fields game, uid; where category = 1 & uid = ({idsString}); limit {batchSize};";

                try
                {
                    var responseJson = await _igdbClient.ApiRequestAsync("external_games", query);
                    if (!string.IsNullOrEmpty(responseJson))
                    {
                        var externalGames = JsonConvert.DeserializeObject<List<IgdbExternalGameDto>>(responseJson);
                        if (externalGames != null)
                        {
                            foreach (var item in externalGames)
                            {
                                foundGameIds.Add(item.Game);
                                mappedSteamIds.Add(item.Uid); // Zapamiętujemy, że to Steam ID już mamy
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd podczas importu ID: {ex.Message}");
                }
            }

            // --- ETAP 2: Fallback - Wyszukiwanie po nazwie ---
            // Dla gier, których NIE znaleźliśmy w Etapie 1, robimy wyszukiwanie po nazwie.
            // Jest to wolniejsze (jedno zapytanie na grę), ale rozwiązuje problem brakujących linków.

            var notFoundSteamGames = steamGames
                .Where(g => !mappedSteamIds.Contains(g.AppId.ToString()))
                .ToList();

            // Ograniczamy liczbę wyszukiwań dla bezpieczeństwa (np. max 20-30 na raz, żeby nie zablokować serwera)
            // lub po prostu robimy to z małym opóźnieniem.

            foreach (var game in notFoundSteamGames)
            {
                // Pomijamy gry z bardzo krótkimi nazwami lub dziwnymi znakami, które mogą psuć zapytania
                if (string.IsNullOrWhiteSpace(game.Name) || game.Name.Length < 2) continue;

                var cleanName = CleanSteamGameName(game.Name);

                // Szukamy po nazwie, bierzemy pierwszy najlepszy wynik
                var query = $"fields id; search \"{cleanName}\"; limit 1;";

                try
                {
                    // Małe opóźnienie, aby nie przekroczyć limitów API (np. 4 zapytania na sekundę dla free tier)
                    await Task.Delay(250);

                    var responseJson = await _igdbClient.ApiRequestAsync("games", query);
                    if (!string.IsNullOrEmpty(responseJson))
                    {
                        // Używamy tej samej klasy ApiGame co w innych miejscach, ale potrzebujemy tylko ID
                        var searchResults = JsonConvert.DeserializeObject<List<ApiGame>>(responseJson);
                        var bestMatch = searchResults?.FirstOrDefault();

                        if (bestMatch != null)
                        {
                            foundGameIds.Add(bestMatch.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd podczas wyszukiwania gry '{game.Name}': {ex.Message}");
                }
            }

            // --- ETAP 3: Zapis do bazy ---

            // Filtrujemy tylko te ID, których użytkownik jeszcze nie ma
            var newIdsToAdd = foundGameIds.Where(id => !ownedIgdbIds.Contains(id)).ToList();

            if (!newIdsToAdd.Any())
            {
                TempData["StatusMessage"] = "Nie znaleziono nowych gier do dodania (wszystkie zidentyfikowane już posiadasz).";
                return RedirectToAction("SteamLibrary");
            }

            var newLibraryEntries = newIdsToAdd.Select(igdbId => new GameInLibrary
            {
                UserId = user.Id,
                IgdbGameId = igdbId,
                DateAddedToLibrary = DateTime.Now,
                CurrentUserStoryProgressPercent = 0
            }).ToList();

            await _context.GamesInLibraries.AddRangeAsync(newLibraryEntries);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = $"Sukces! Dodano {newLibraryEntries.Count} nowych gier do Twojej biblioteki.";

            // Jeśli dużo gier nie zostało znalezionych, możemy o tym poinformować (opcjonalne)
            if (foundGameIds.Count < steamGames.Count)
            {
                int missingCount = steamGames.Count - foundGameIds.Count;
                // Możesz odkomentować poniższą linię, jeśli chcesz szczegółowy komunikat:
                // TempData["StatusMessage"] += $" Nie udało się dopasować automacznie {missingCount} gier.";
            }

            return RedirectToAction("SteamLibrary");
        }

        // Metoda pomocnicza do czyszczenia nazwy gry przed wyszukiwaniem
        private string CleanSteamGameName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";

            // Usuwamy znaki specjalne, które mogą mylić wyszukiwarkę IGDB lub psuć składnię
            // np. "Half-Life 2" -> "Half Life 2" jest bezpieczniejsze w search
            // oraz usuwamy znaki TM, R, itp.
            var clean = name.Replace("™", "")
                            .Replace("®", "")
                            .Replace("©", "")
                            .Replace("\"", "") // Usuwamy cudzysłowy, bo psują zapytanie
                            .Trim();

            return clean;
        }

        // Klasa DTO potrzebna do odczytu external_games (musi być wewnątrz klasy lub namespace)
        private class IgdbExternalGameDto
        {
            [JsonProperty("game")]
            public long Game { get; set; }

            [JsonProperty("uid")]
            public string Uid { get; set; }
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

        // GET: Profile/GetBanner/GUID
        [AllowAnonymous]
        public async Task<IActionResult> GetBanner(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user != null && user.Banner != null && !string.IsNullOrEmpty(user.BannerContentType))
            {
                return File(user.Banner, user.BannerContentType);
            }

            var defaultBannerPath = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "banners", "default_banner.png");
            if (!System.IO.File.Exists(defaultBannerPath)) return NotFound();
            return PhysicalFile(defaultBannerPath, "image/png");
        }


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

        // POST: /Profile/ChangeBanner
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeBanner(IFormFile bannerFile)
        {
            if (bannerFile == null || bannerFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Nie wybrano pliku.";
                return RedirectToAction(nameof(Index));
            }

            var user = await GetCurrentUserAsync();
            if (user == null) return NotFound();

            if (bannerFile.Length > 5 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "Plik jest za duży (maksymalnie 5MB).";
                return RedirectToAction(nameof(Index));
            }
            var allowedTypes = new[] { "image/jpeg", "image/png" };
            if (!allowedTypes.Contains(bannerFile.ContentType))
            {
                TempData["ErrorMessage"] = "Nieprawidłowy format pliku (dozwolone JPG i PNG).";
                return RedirectToAction(nameof(Index));
            }

            using (var memoryStream = new MemoryStream())
            {
                await bannerFile.CopyToAsync(memoryStream);
                user.Banner = memoryStream.ToArray();
                user.BannerContentType = bannerFile.ContentType;
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["StatusMessage"] = "Baner został zaktualizowany.";
            }
            else
            {
                TempData["ErrorMessage"] = "Błąd podczas aktualizacji banera.";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Profile/UpdateDescription
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDescription(UpdateDescriptionViewModel updateDescriptionModel)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Opis jest nieprawidłowy (za długi?).";
                return RedirectToAction(nameof(Index));
            }

            var user = await GetCurrentUserAsync();
            if (user == null) return NotFound();

            user.Description = updateDescriptionModel.Description;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["StatusMessage"] = "Opis został zaktualizowany.";
            }
            else
            {
                TempData["ErrorMessage"] = "Błąd podczas aktualizacji opisu.";
            }

            return RedirectToAction(nameof(Index));
        }

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
        // --- NOWA METODA: Pobieranie okładki z IGDB na żądanie (AJAX) ---
        [HttpGet]
        public async Task<IActionResult> GetIgdbCover(string steamId, string gameName)
        {
            if (string.IsNullOrEmpty(steamId)) return Json(new { url = "" });

            string coverUrl = null;

            // 1. Próba znalezienia po Steam ID (external_games)
            try
            {
                // Pytamy o pole game.cover.url
                var query = $"fields game.cover.url; where category = 1 & uid = \"{steamId}\"; limit 1;";
                var json = await _igdbClient.ApiRequestAsync("external_games", query);

                if (!string.IsNullOrEmpty(json))
                {
                    // Używamy dynamicznej deserializacji dla prostoty struktury zagnieżdżonej
                    var result = JsonConvert.DeserializeObject<List<dynamic>>(json);
                    if (result != null && result.Count > 0)
                    {
                        string url = result[0]?.game?.cover?.url;
                        if (!string.IsNullOrEmpty(url))
                        {
                            coverUrl = url;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd pobierania okładki po ID: {ex.Message}");
            }

            // 2. Fallback: Jeśli po ID się nie udało, szukamy po nazwie
            if (string.IsNullOrEmpty(coverUrl) && !string.IsNullOrEmpty(gameName))
            {
                try
                {
                    var cleanName = CleanSteamGameName(gameName); // Używamy Twojej metody pomocniczej
                    var query = $"fields cover.url; search \"{cleanName}\"; limit 1;";

                    var json = await _igdbClient.ApiRequestAsync("games", query);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var result = JsonConvert.DeserializeObject<List<dynamic>>(json);
                        string url = result?.FirstOrDefault()?.cover?.url;
                        if (!string.IsNullOrEmpty(url))
                        {
                            coverUrl = url;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Błąd pobierania okładki po nazwie: {ex.Message}");
                }
            }

            // Jeśli znaleźliśmy URL, zamieniamy miniaturkę na dużą okładkę
            if (!string.IsNullOrEmpty(coverUrl))
            {
                coverUrl = coverUrl.Replace("t_thumb", "t_cover_big");
                // Dodajemy 'https:' jeśli brakuje (IGDB zwraca linki zaczynające się od //)
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

            // Usuń powiązane dane (Gry, Osiągnięcia itp.)
            // Uwaga: EF Core z konfiguracją Cascade Delete powinien to obsłużyć, 
            // ale dla pewności można wyczyścić ręcznie w LibraryController logic lub tutaj.
            // Zakładamy, że baza ma CascadeDelete.

            await _signInManager.SignOutAsync();
            await _userManager.DeleteAsync(user);

            return RedirectToAction("Index", "Home");
        }
    }
}