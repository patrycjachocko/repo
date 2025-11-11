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
using Microsoft.AspNetCore.Identity.UI.Services;
using System.Text.Encodings.Web;

namespace praca_dyplomowa_zesp.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IEmailSender _emailSender;

        public ProfileController(UserManager<User> userManager,
                                 SignInManager<User> signInManager,
                                 IWebHostEnvironment webHostEnvironment,
                                 IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _webHostEnvironment = webHostEnvironment;
            _emailSender = emailSender;
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
                Email = user.Email,
                Description = user.Description
            };

            ViewData["StatusMessage"] = TempData["StatusMessage"];
            ViewData["ErrorMessage"] = TempData["ErrorMessage"];

            return View(viewModel);
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

        // POST: /Profile/ChangeEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeEmail([Bind(Prefix = "changeEmailModel")] ChangeEmailViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Nowy adres e-mail jest nieprawidłowy.";
                return RedirectToAction(nameof(Index));
            }

            var user = await GetCurrentUserAsync();
            if (user == null) return NotFound();

            if (model.NewEmail == user.Email)
            {
                TempData["ErrorMessage"] = "To jest już Twój obecny adres e-mail.";
                return RedirectToAction(nameof(Index));
            }

            var token = await _userManager.GenerateChangeEmailTokenAsync(user, model.NewEmail);
            var callbackUrl = Url.Action("ConfirmEmailChange", "Account",
                new { userId = user.Id, email = model.NewEmail, code = token },
                protocol: Request.Scheme);

            // ***** POCZĄTEK ZMIANY (E-mail) *****
            // Wysyłamy e-mail na STARY (user.Email), a nie na nowy (model.NewEmail)
            await _emailSender.SendEmailAsync(user.Email, "Potwierdź zmianę adresu e-mail",
                $"Jeżeli chcesz zmienić mail konta na: {model.NewEmail} to kliknij poniższy link: <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>link</a>.");

            // Poprawiono komunikat TempData
            TempData["StatusMessage"] = "Link potwierdzający zmianę adresu e-mail został wysłany na Twój *obecny* adres e-mail.";
            // ***** KONIEC ZMIANY (E-mail) *****

            return RedirectToAction(nameof(Index));
        }
    }
}