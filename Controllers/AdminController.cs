using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa_zesp.Models.Modules.Admin;
using praca_dyplomowa_zesp.Models.Users;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace praca_dyplomowa_zesp.Controllers
{
    [Authorize(Roles = "Admin")] // Tylko dla admina
    public class AdminController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;

        public AdminController(UserManager<User> userManager, RoleManager<IdentityRole<Guid>> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: /Admin
        public async Task<IActionResult> Index(string searchString)
        {
            // 1. Pobierz query użytkowników (z include ról jeśli potrzebne, ale Identity robi to osobno)
            var usersQuery = _userManager.Users.AsQueryable();

            // 2. Wyszukiwanie
            if (!string.IsNullOrEmpty(searchString))
            {
                usersQuery = usersQuery.Where(u => u.UserName.Contains(searchString) || u.Email.Contains(searchString));
            }

            var usersList = await usersQuery.ToListAsync();
            var model = new AdminPanelViewModel
            {
                SearchString = searchString,
                Users = new List<AdminUserDto>()
            };

            // 3. Mapowanie na DTO
            foreach (var user in usersList)
            {
                var roles = await _userManager.GetRolesAsync(user);

                bool isLocked = await _userManager.IsLockedOutAsync(user);
                bool isMuted = user.BanEnd.HasValue && user.BanEnd.Value > DateTimeOffset.Now;

                model.Users.Add(new AdminUserDto
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Login = user.Login, // ZMIANA: Przypisujemy Login zamiast Emaila
                    Roles = roles.ToList(),
                    IsLockedOut = isLocked,
                    LockoutEnd = user.LockoutEnd,
                    IsMuted = isMuted,
                    MuteEnd = user.BanEnd,
                    AvatarUrl = user.ProfilePicture != null
                        ? $"data:{user.ProfilePictureContentType};base64,{Convert.ToBase64String(user.ProfilePicture)}"
                        : "/uploads/avatars/default_avatar.png"
                });
            }

            return View(model);
        }

        // AKCJA: Zmień rolę (Promuj/Degraduj)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleModerator(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound();

            // Zabezpieczenie: Nie można zmienić roli admina głównego lub siebie samego
            if (user.UserName == "Admin" || user.UserName == User.Identity.Name)
            {
                TempData["Error"] = "Nie możesz zmienić uprawnień tego użytkownika.";
                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.IsInRoleAsync(user, "Moderator"))
            {
                await _userManager.RemoveFromRoleAsync(user, "Moderator");
                TempData["Success"] = $"Odebrano uprawnienia Moderatora użytkownikowi {user.UserName}.";
            }
            else
            {
                await _userManager.AddToRoleAsync(user, "Moderator");
                TempData["Success"] = $"Nadano uprawnienia Moderatora użytkownikowi {user.UserName}.";
            }

            return RedirectToAction(nameof(Index));
        }

        // AKCJA: Ban (Blokada logowania - Lockout)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BanUser(Guid userId, int days, string reason)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound();

            if (user.UserName == "Admin" || user.UserName == User.Identity.Name)
            {
                TempData["Error"] = "Nie można zbanować tego użytkownika.";
                return RedirectToAction(nameof(Index));
            }

            if (days > 0)
            {
                var banUntil = DateTimeOffset.Now.AddDays(days);
                await _userManager.SetLockoutEndDateAsync(user, banUntil);
                await _userManager.SetLockoutEnabledAsync(user, true);

                // Opcjonalnie zapisz powód w custom polu
                user.BanReason = reason;
                await _userManager.UpdateAsync(user);

                TempData["Success"] = $"Użytkownik {user.UserName} został zbanowany na {days} dni.";
            }
            else
            {
                // Odbanowanie
                await _userManager.SetLockoutEndDateAsync(user, null);
                user.BanReason = null;
                await _userManager.UpdateAsync(user);
                TempData["Success"] = $"Użytkownik {user.UserName} został odbanowany.";
            }

            return RedirectToAction(nameof(Index));
        }

        // AKCJA: Mute (Wyciszenie - Custom BanEnd)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MuteUser(Guid userId, int hours)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound();

            if (hours > 0)
            {
                user.BanEnd = DateTimeOffset.Now.AddHours(hours);
                // Możesz użyć isBanned jako flagi pomocniczej, jeśli chcesz
                user.isBanned = true;

                await _userManager.UpdateAsync(user);
                TempData["Success"] = $"Użytkownik {user.UserName} został wyciszony na {hours} godzin.";
            }
            else
            {
                // Odciszenie
                user.BanEnd = null;
                user.isBanned = false;
                await _userManager.UpdateAsync(user);
                TempData["Success"] = $"Użytkownik {user.UserName} został odciszony.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}