using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using praca_dyplomowa_zesp.Models.Users;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication; // Do obsługi logowania zewnętrznego
using System.Security.Claims; // Do wyciągania ID z ciasteczek
using praca_dyplomowa_zesp.Models.API;

namespace praca_dyplomowa_zesp.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly SteamApiService _steamService; // Serwis Steam

        public ProfileController(UserManager<User> userManager,
                                 SignInManager<User> signInManager,
                                 IWebHostEnvironment webHostEnvironment,
                                 SteamApiService steamService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _webHostEnvironment = webHostEnvironment;
            _steamService = steamService;
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

            var viewModel = new ProfileViewModel
            {
                UserId = user.Id,
                Username = user.UserName,
                Description = user.Description,
                SteamId = user.SteamId // Przekazujemy SteamId do widoku
            };

            ViewData["StatusMessage"] = TempData["StatusMessage"];
            ViewData["ErrorMessage"] = TempData["ErrorMessage"];

            return View(viewModel);
        }

        // --- SEKCJA STEAM ---

        [HttpPost]
        public IActionResult LinkSteam()
        {
            // Przekierowanie do logowania Steam
            var redirectUrl = Url.Action("LinkSteamCallback", "Profile");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Steam", redirectUrl);
            return new ChallengeResult("Steam", properties);
        }

        [HttpGet]
        public async Task<IActionResult> LinkSteamCallback()
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return RedirectToAction("Index");

            // Pobieramy dane logowania zewnętrznego
            var info = await _signInManager.GetExternalLoginInfoAsync(user.Id.ToString());

            // Jeśli standardowa metoda zawiedzie (częste przy łączeniu kont już zalogowanych), próbujemy manualnie
            if (info == null)
            {
                var result = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
                if (result?.Succeeded == true)
                {
                    var steamIdClaim = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (steamIdClaim != null)
                    {
                        var steamId = steamIdClaim.Split('/').Last(); // Wyciągamy ID z URL
                        user.SteamId = steamId;
                        await _userManager.UpdateAsync(user);
                        TempData["StatusMessage"] = "Konto Steam zostało połączone!";
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "Błąd podczas łączenia ze Steam.";
                }
            }
            else
            {
                var steamIdClaim = info.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var steamId = steamIdClaim.Split('/').Last();
                user.SteamId = steamId;
                await _userManager.UpdateAsync(user);
                TempData["StatusMessage"] = "Konto Steam zostało połączone!";
            }

            return RedirectToAction("Index");
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
            // Sortujemy gry malejąco wg czasu gry
            return View(games.OrderByDescending(g => g.PlaytimeForever).ToList());
        }

        // --- KONIEC SEKCJI STEAM ---

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

            if (avatarFile.Length > 2 * 1024 * 1024) // 2 MB
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

            if (bannerFile.Length > 5 * 1024 * 1024) // 5 MB
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
    }
}