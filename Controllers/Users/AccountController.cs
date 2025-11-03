using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IEmailSender _emailSender;

        public AccountController(UserManager<User> userManager, SignInManager<User> signInManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new User
                {
                    UserName = model.UserName,
                    Email = model.Email,
                    Login = model.Login,
                    CreatedAt = DateTime.UtcNow,
                    LastActive = DateTime.UtcNow,
                    Role = "User"
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                    var confirmationLink = Url.Action("MailConfirmation", "Account",
                        new { userId = user.Id, token = token }, Request.Scheme);

                    await _emailSender.SendEmailAsync(model.Email,
                        "Potwierdź swoje konto",
                        $"Potwierdź swoje konto, klikając ten link: <a href='{confirmationLink}'>Link</a>");

                    return RedirectToAction("RegisterConfirmation");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult RegisterConfirmation()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> MailConfirmation(string userId, string token)
        {
            if (userId == null || token == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                ViewData["ErrorMessage"] = "Nie znaleziono użytkownika o podanym ID.";
                return View("Error"); // Upewnij się, że masz widok Error
            }

            // Sprawdź token i oznacz e-mail jako potwierdzony
            var result = await _userManager.ConfirmEmailAsync(user, token);

            if (result.Succeeded)
            {
                // Pokaż widok "ConfirmEmail.cshtml"
                return View();
            }

            ViewData["ErrorMessage"] = "Błąd podczas potwierdzania adresu e-mail.";
            return View("Error");
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Login, model.Password, false, lockoutOnFailure: true); // w trzecim arg jak dodamy mozliwosc zapamietania zalogowania to mozna od tego uzaleznic

                if (result.Succeeded)
                {
                    return RedirectToAction("Index", "Home");
                }
                if (result.IsNotAllowed)
                {
                    ModelState.AddModelError(string.Empty, "Twoje konto nie zostało jeszcze potwierdzone. Sprawdź e-mail.");
                    return View(model);
                }
                ModelState.AddModelError(string.Empty, "Nieprawidłowy login lub hasło.");


            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}