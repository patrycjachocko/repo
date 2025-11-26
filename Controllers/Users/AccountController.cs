using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using praca_dyplomowa_zesp.Models.Users;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Text.Encodings.Web;
using praca_dyplomowa.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using praca_dyplomowa_zesp.Models.API;

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
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _steamService = steamService;
        }

        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = await _context.Users.SingleOrDefaultAsync(u => u.Login == model.Login);

                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Nieprawidłowa próba logowania.");
                    return View(model);
                }

                var result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    return RedirectToLocal(returnUrl);
                }
                if (result.IsLockedOut)
                {
                    return View("Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Nieprawidłowa próba logowania.");
                    return View(model);
                }
            }

            return View(model);
        }

        // GET: /Account/Register
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                if (await _context.Users.AnyAsync(u => u.Login == model.Login))
                {
                    ModelState.AddModelError(nameof(model.Login), "Ten login jest już zajęty.");
                    return View(model);
                }

                var user = new User
                {
                    UserName = model.UserName,
                    // Email = model.Email, <- USUNIĘTO
                    Login = model.Login,
                    CreatedAt = DateTime.Now
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // USUNIĘTO: Generowanie tokenu i wysyłanie e-maila

                    // ZMIANA: Automatyczne logowanie po rejestracji (skoro nie ma potwierdzenia email)
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToLocal(returnUrl);
                }
                AddErrors(result);
            }

            return View(model);
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        // 1. Inicjacja logowania Steam (kliknięcie w guzik)
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult LoginWithSteam(string returnUrl = null)
        {
            var redirectUrl = Url.Action("SteamLoginCallback", "Account", new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Steam", redirectUrl);
            return Challenge(properties, "Steam");
        }

        // 2. Powrót ze Steam (Callback)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> SteamLoginCallback(string returnUrl = null, string remoteError = null)
        {
            if (remoteError != null)
            {
                return RedirectToAction(nameof(Login));
            }

            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                return RedirectToAction(nameof(Login));
            }

            // Wydobycie SteamID z Claims
            var steamIdClaim = info.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            // SteamID w claimie wygląda np. tak: "https://steamcommunity.com/openid/id/76561198000000000"
            // Musimy wyciąć sam numer
            var steamId = steamIdClaim?.Split('/').LastOrDefault();

            if (string.IsNullOrEmpty(steamId))
            {
                return RedirectToAction(nameof(Login));
            }

            // A. Sprawdź, czy mamy już użytkownika z tym SteamID w bazie
            var user = await _context.Users.FirstOrDefaultAsync(u => u.SteamId == steamId);

            if (user != null)
            {
                // Użytkownik istnieje -> Logujemy go
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToLocal(returnUrl);
            }

            // B. Użytkownika nie ma -> Tworzymy nowe konto
            // Najpierw pobierzmy dane ze Steam (nick, awatar)
            var steamProfile = await _steamService.GetPlayerSummaryAsync(steamId);
            string nick = steamProfile?.PersonaName ?? $"Gracz_{steamId.Substring(0, 5)}";

            // Generujemy unikalny login i hasło (użytkownik ich nie zna)
            var uniqueLogin = "Steam_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var randomPassword = "SteamPassword!" + Guid.NewGuid().ToString();

            // Upewnijmy się, że UserName (nick) jest unikalny w bazie identity
            var finalUserName = nick;
            int counter = 1;
            while (await _userManager.FindByNameAsync(finalUserName) != null)
            {
                finalUserName = $"{nick}{counter++}";
            }

            var newUser = new User
            {
                UserName = finalUserName,
                Login = uniqueLogin, // Techniczny login
                SteamId = steamId,
                CreatedAt = DateTime.Now,
                EmailConfirmed = true // Uznajemy, że Steam potwierdza tożsamość
            };

            // Pobieranie awatara
            if (!string.IsNullOrEmpty(steamProfile?.AvatarFullUrl))
            {
                var avatarBytes = await _steamService.DownloadAvatarAsync(steamProfile.AvatarFullUrl);
                if (avatarBytes != null)
                {
                    newUser.ProfilePicture = avatarBytes;
                    newUser.ProfilePictureContentType = "image/jpeg"; // Steam zazwyczaj zwraca JPG
                }
            }

            var createResult = await _userManager.CreateAsync(newUser, randomPassword);
            if (createResult.Succeeded)
            {
                // Dodajemy logowanie zewnętrzne (opcjonalne, ale dobra praktyka w Identity)
                await _userManager.AddLoginAsync(newUser, info);

                // Logujemy użytkownika
                await _signInManager.SignInAsync(newUser, isPersistent: false);
                return RedirectToLocal(returnUrl);
            }

            // Jeśli coś poszło nie tak
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View("Login");
        }

    }
}