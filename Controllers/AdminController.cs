using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa.Data; // Upewnij się, że ten namespace jest poprawny dla Twojego DbContext
using praca_dyplomowa_zesp.Models.Modules.Admin;
using praca_dyplomowa_zesp.Models.Modules.Guides;
using praca_dyplomowa_zesp.Models.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace praca_dyplomowa_zesp.Controllers
{
    [Authorize(Roles = "Admin,Moderator")] // Pozwalamy też Moderatorom (zgodnie z Twoim opisem)
    public class AdminController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly ApplicationDbContext _context; // Dodano kontekst bazy

        public AdminController(UserManager<User> userManager, RoleManager<IdentityRole<Guid>> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // GET: /Admin
        public async Task<IActionResult> Index(string searchString)
        {
            var model = new AdminPanelViewModel
            {
                SearchString = searchString,
                Users = new List<AdminUserDto>(),
                PendingGuides = new List<Guide>() // Inicjalizacja pustej listy
            };

            // 1. UŻYTKOWNICY - Pobieramy TYLKO jeśli użytkownik jest Adminem
            if (User.IsInRole("Admin"))
            {
                var usersQuery = _userManager.Users.AsQueryable();

                if (!string.IsNullOrEmpty(searchString))
                {
                    usersQuery = usersQuery.Where(u => u.UserName.Contains(searchString) || u.Email.Contains(searchString));
                }

                var usersList = await usersQuery.ToListAsync();

                // Mapowanie użytkowników na DTO
                foreach (var user in usersList)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    bool isLocked = await _userManager.IsLockedOutAsync(user);
                    bool isMuted = user.BanEnd.HasValue && user.BanEnd.Value > DateTimeOffset.Now;

                    model.Users.Add(new AdminUserDto
                    {
                        Id = user.Id,
                        UserName = user.UserName,
                        Login = user.Login,
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
            }

            // 2. POSTY - Pobieramy zawsze (dla Admina i Moderatora)
            // Logika bez zmian
            var pendingGuides = await _context.Guides
                .Include(g => g.User)
                .Where(g => !g.IsApproved)
                .OrderBy(g => g.CreatedAt)
                .ToListAsync();

            model.PendingGuides = pendingGuides;

            return View(model);
        }

        // --- ZARZĄDZANIE POSTAMI ---

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveGuide(int id)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide == null) return NotFound();

            guide.IsApproved = true;
            _context.Update(guide);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Zaakceptowano poradnik: {guide.Title}";
            return RedirectToAction(nameof(Index)); // Wracamy do panelu (zakładka powinna się przełączyć JS-em lub zostać na głównej)
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectGuide(int id)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide == null) return NotFound();

            // Odrzucenie = Usunięcie (można ewentualnie dodać pole "IsRejected" i powód, ale na razie usuwamy)
            _context.Guides.Remove(guide);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Odrzucono (usunięto) poradnik: {guide.Title}";
            return RedirectToAction(nameof(Index));
        }


        // --- ZARZĄDZANIE UŻYTKOWNIKAMI (Bez zmian logicznych, tylko autoryzacja Admin) ---

        [Authorize(Roles = "Admin")] // Tylko Admin może zmieniać moderatorów
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleModerator(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound();

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

        [Authorize(Roles = "Admin")] // Tylko Admin może banować (chyba że chcesz inaczej)
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

                user.BanReason = reason;
                await _userManager.UpdateAsync(user);

                TempData["Success"] = $"Użytkownik {user.UserName} został zbanowany na {days} dni.";
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                user.BanReason = null;
                await _userManager.UpdateAsync(user);
                TempData["Success"] = $"Użytkownik {user.UserName} został odbanowany.";
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MuteUser(Guid userId, int hours)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null) return NotFound();

            if (hours > 0)
            {
                user.BanEnd = DateTimeOffset.Now.AddHours(hours);
                user.isBanned = true; // Flaga pomocnicza
                await _userManager.UpdateAsync(user);
                TempData["Success"] = $"Użytkownik {user.UserName} został wyciszony na {hours} godzin.";
            }
            else
            {
                user.BanEnd = null;
                user.isBanned = false;
                await _userManager.UpdateAsync(user);
                TempData["Success"] = $"Użytkownik {user.UserName} został odciszony.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}