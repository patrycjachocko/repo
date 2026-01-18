using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using praca_dyplomowa_zesp.Services;
using praca_dyplomowa_zesp.Data;
using praca_dyplomowa_zesp.Models.Modules.Users;
using praca_dyplomowa_zesp.Models.ViewModels.Users;

namespace praca_dyplomowa_zesp.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly SteamApiService _steamService;

        public AccountController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ApplicationDbContext context,
            SteamApiService steamService)
        {
            //przypisanie wstrzyknietych zaleznosci do pol klasy
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _steamService = steamService;
        }

        #region Standard Authentication

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (TempData["Error"] != null)
            {
                //przekazanie bledu z sesji do stanu modelu widoku
                ModelState.AddModelError(string.Empty, TempData["Error"].ToString());
            }

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            //pobranie uzytkownika z bazy na podstawie wlasnego pola login
            var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == model.Login);

            if (user == null)
            {
                SetLoginError("Nieprawidłowy login lub hasło.");
                return View(model);
            }

            if (user.isBanned)
            {
                if (user.BanEnd.HasValue && user.BanEnd > DateTimeOffset.Now)
                {
                    string errorMsg = GetBanMessage(user);
                    SetLoginError(errorMsg);
                    return View(model);
                }

                //wyczyszczenie danych o blokadzie jesli czas kary juz minal
                user.isBanned = false;
                user.BanEnd = null;
                user.BanReason = null;
                await _userManager.UpdateAsync(user);
            }

            //logowanie z wykorzystaniem wbudowanego managera identity
            var result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                //odnotowanie czasu ostatniego wejscia uzytkownika do systemu
                user.LastActive = DateTime.Now;
                await _userManager.UpdateAsync(user);
                return RedirectToLocal(returnUrl);
            }

            if (result.IsLockedOut)
            {
                SetLoginError("Konto zostało tymczasowo zablokowane z powodu zbyt wielu nieudanych prób.");
                return View(model);
            }

            SetLoginError("Nieprawidłowy login lub hasło.");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(string? returnUrl = null)
        {
            await _signInManager.SignOutAsync();

            if (returnUrl != null && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        #endregion

        #region Registration

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            //reczna walidacja unikalnosci pola login przed utworzeniem konta
            if (await _context.Users.AnyAsync(u => u.Login == model.Login))
            {
                ModelState.AddModelError(nameof(model.Login), "Ten login jest już zajęty.");
                TempData["Error"] = "Ten login jest już zajęty.";
                return View(model);
            }

            var user = new User
            {
                UserName = model.UserName,
                Login = model.Login,
                CreatedAt = DateTime.Now,
                Role = "User"
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                //przypisanie domyslnej roli systemowej dla nowego profilu
                await _userManager.AddToRoleAsync(user, "User");
                await _signInManager.SignInAsync(user, isPersistent: false);
                TempData["Success"] = "Rejestracja udana! Witaj w GameHub.";
                return RedirectToLocal(returnUrl);
            }

            AddErrors(result);
            return View(model);
        }

        #endregion

        #region Steam Integration

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult LoginWithSteam(string returnUrl = null)
        {
            //konfiguracja parametrów dla zewnętrznego dostawcy tozsamosci
            var redirectUrl = Url.Action("SteamLoginCallback", "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Steam", redirectUrl);
            return Challenge(properties, "Steam");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> SteamLoginCallback(string returnUrl = null, string remoteError = null)
        {
            if (remoteError != null) return RedirectToAction(nameof(Login));

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null) return RedirectToAction(nameof(Login));

            //wydobycie 64-bitowego identyfikatora z adresu url zwroconego przez steam
            var steamIdClaim = info.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var steamId = steamIdClaim?.Split('/').LastOrDefault();

            if (string.IsNullOrEmpty(steamId)) return RedirectToAction(nameof(Login));

            var user = await _context.Users.FirstOrDefaultAsync(u => u.SteamId == steamId);

            if (user != null)
            {
                if (user.isBanned && user.BanEnd.HasValue && user.BanEnd > DateTimeOffset.Now)
                {
                    TempData["Error"] = GetBanMessage(user);
                    return RedirectToAction(nameof(Login));
                }

                user.LastActive = DateTime.Now;
                await _userManager.UpdateAsync(user);
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToLocal(returnUrl);
            }

            //pobranie szczegolowych informacji o profilu przez api steam
            var steamProfile = await _steamService.GetPlayerSummaryAsync(steamId);
            string nick = steamProfile?.PersonaName ?? $"Gracz_{steamId.Substring(0, 5)}";

            //wywolanie funkcji zapewniajacych brak konfliktow nazw w bazie
            string finalUserName = await GenerateUniqueUserName(nick);
            string finalLogin = await GenerateUniqueLogin(nick);

            var newUser = new User
            {
                UserName = finalUserName,
                Login = finalLogin,
                SteamId = steamId,
                CreatedAt = DateTime.Now,
                Role = "User",
                LastActive = DateTime.Now
            };

            if (!string.IsNullOrEmpty(steamProfile?.AvatarFullUrl))
            {
                //konwersja zdjecia profilowego ze steam na format binarny do bazy
                var avatarBytes = await _steamService.DownloadAvatarAsync(steamProfile.AvatarFullUrl);
                if (avatarBytes != null)
                {
                    newUser.ProfilePicture = avatarBytes;
                    newUser.ProfilePictureContentType = "image/jpeg";
                }
            }

            var createResult = await _userManager.CreateAsync(newUser);
            if (createResult.Succeeded)
            {
                await _userManager.AddToRoleAsync(newUser, "User");
                await _userManager.AddLoginAsync(newUser, info);
                await _signInManager.SignInAsync(newUser, isPersistent: false);
                return RedirectToLocal(returnUrl);
            }

            AddErrors(createResult);
            return View("Login");
        }

        #endregion

        #region Helpers

        private async Task<string> GenerateUniqueUserName(string baseName)
        {
            string name = baseName;
            int counter = 1;
            //iteracyjne sprawdzanie dostepnosci nazwy i dodawanie numeracji
            while (await _userManager.FindByNameAsync(name) != null)
            {
                name = $"{baseName}{counter++}";
            }
            return name;
        }

        private async Task<string> GenerateUniqueLogin(string baseLogin)
        {
            string login = baseLogin;
            int counter = 1;
            //analogiczne zapewnienie unikalnosci dla pola login
            while (await _context.Users.AnyAsync(u => u.Login == login))
            {
                login = $"{baseLogin}{counter++}";
            }
            return login;
        }

        private void SetLoginError(string message)
        {
            //jednoczesne ustawienie bledu w modelu i powiadomienia tymczasowego
            ModelState.AddModelError(string.Empty, message);
            TempData["Error"] = message;
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private string GetBanMessage(User user)
        {
            string errorMsg = "Twoje konto zostało zablokowane.";
            if (user.BanEnd.HasValue)
            {
                //formatowanie daty wygasniecia blokady na czytelny format lokalny
                var endDate = user.BanEnd.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
                errorMsg = $"Twoje konto jest zbanowane do: {endDate}.";
                if (!string.IsNullOrEmpty(user.BanReason)) errorMsg += $" Powód: {user.BanReason}";
            }
            return errorMsg;
        }

        #endregion
    }
}