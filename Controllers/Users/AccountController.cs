using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using praca_dyplomowa_zesp.Models.Users;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Text.Encodings.Web;
using praca_dyplomowa.Data;
using Microsoft.EntityFrameworkCore;
using System;

namespace praca_dyplomowa_zesp.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        // USUNIĘTO: private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;

        public AccountController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            // IEmailSender emailSender, <- USUNIĘTO
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            // _emailSender = emailSender; <- USUNIĘTO
            _context = context;
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

        // USUNIĘTO: RegisterConfirmation
        // USUNIĘTO: ConfirmEmail
        // USUNIĘTO: ConfirmEmailChange
        // USUNIĘTO: MailConfirmation

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction(nameof(HomeController.Index), "Home");
            }
        }
    }
}