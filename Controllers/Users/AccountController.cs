using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using praca_dyplomowa_zesp.Models.Users;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using praca_dyplomowa.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Linq;
using praca_dyplomowa_zesp.Models.API;
using praca_dyplomowa_zesp.Controllers;

namespace praca_dyplomowa_zesp.Controllers.Users
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
            if (TempData["Error"] != null)
            {
                ModelState.AddModelError(string.Empty, TempData["Error"].ToString());
            }
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

                var result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: true);

                if (result.Succeeded)
                {
                    user.LastActive = DateTime.Now;
                    await _userManager.UpdateAsync(user);
                    return RedirectToLocal(returnUrl);
                }

                if (result.IsLockedOut)
                {
                    string errorMsg = GetBanMessage(user);
                    ModelState.AddModelError(string.Empty, errorMsg);
                    return View(model);
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
                    UserName = model.UserName, // Wyświetlana nazwa
                    Login = model.Login,       // Login do logowania
                    CreatedAt = DateTime.Now,
                    EmailConfirmed = true,
                    Role = "User"
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "User");
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

        // --- STEAM ---

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public IActionResult LoginWithSteam(string returnUrl = null)
        {
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

            var steamIdClaim = info.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var steamId = steamIdClaim?.Split('/').LastOrDefault();

            if (string.IsNullOrEmpty(steamId)) return RedirectToAction(nameof(Login));

            // A. UŻYTKOWNIK ISTNIEJE
            var user = await _context.Users.FirstOrDefaultAsync(u => u.SteamId == steamId);

            if (user != null)
            {
                if (await _userManager.IsLockedOutAsync(user))
                {
                    TempData["Error"] = GetBanMessage(user);
                    return RedirectToAction(nameof(Login));
                }

                user.LastActive = DateTime.Now;
                await _userManager.UpdateAsync(user);
                await _signInManager.SignInAsync(user, isPersistent: false);
                return RedirectToLocal(returnUrl);
            }

            // B. NOWY UŻYTKOWNIK (Steam) - Tworzymy konto
            var steamProfile = await _steamService.GetPlayerSummaryAsync(steamId);

            // Pobieramy nick ze Steam (np. "KoxGamer")
            string nick = steamProfile?.PersonaName ?? $"Gracz_{steamId.Substring(0, 5)}";

            // Upewniamy się, że UserName jest unikalne
            var finalUserName = nick;
            int counter = 1;
            while (await _userManager.FindByNameAsync(finalUserName) != null)
            {
                finalUserName = $"{nick}{counter++}";
            }

            // ZMIANA: Login ustawiamy taki sam jak UserName (jeśli wolny), zamiast "Steam_..."
            // Dzięki temu w bazie będzie ładnie: Login="KoxGamer", UserName="KoxGamer"
            var finalLogin = finalUserName;
            // Sprawdzamy unikalność Loginu (bo w modelu User.Login musi być unikalny)
            counter = 1;
            while (await _context.Users.AnyAsync(u => u.Login == finalLogin))
            {
                finalLogin = $"{nick}{counter++}";
            }

            var randomPassword = "SteamPassword!" + Guid.NewGuid().ToString();

            var newUser = new User
            {
                UserName = finalUserName, // To wyświetlamy (np. KoxGamer)
                Login = finalLogin,       // To zapisujemy jako login
                SteamId = steamId,
                CreatedAt = DateTime.Now,
                EmailConfirmed = true,
                Role = "User",
                LastActive = DateTime.Now
            };

            if (!string.IsNullOrEmpty(steamProfile?.AvatarFullUrl))
            {
                var avatarBytes = await _steamService.DownloadAvatarAsync(steamProfile.AvatarFullUrl);
                if (avatarBytes != null)
                {
                    newUser.ProfilePicture = avatarBytes;
                    newUser.ProfilePictureContentType = "image/jpeg";
                }
            }

            var createResult = await _userManager.CreateAsync(newUser, randomPassword);
            if (createResult.Succeeded)
            {
                await _userManager.AddToRoleAsync(newUser, "User");
                await _userManager.AddLoginAsync(newUser, info);
                await _signInManager.SignInAsync(newUser, isPersistent: false);
                return RedirectToLocal(returnUrl);
            }

            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View("Login");
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

        private string GetBanMessage(User user)
        {
            string errorMsg = "Twoje konto zostało zablokowane.";
            if (user.LockoutEnd.HasValue)
            {
                var endDate = user.LockoutEnd.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
                errorMsg = $"Twoje konto jest zbanowane do: {endDate}.";
                if (!string.IsNullOrEmpty(user.BanReason)) errorMsg += $" Powód: {user.BanReason}";
            }
            return errorMsg;
        }
    }
}