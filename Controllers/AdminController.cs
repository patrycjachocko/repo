using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa.Data; // Upewnij się, że ten namespace jest poprawny dla Twojego DbContext
using praca_dyplomowa_zesp.Models;
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
                PendingGuides = new List<Guide>(),
                DeletedGuides = new List<Guide>(),
                Tickets = new List<Ticket>()
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

            // 2. POSTY DO AKCEPTACJI
            // ZMIANA: Dodano && !g.IsRejected -> Odrzucone znikają z tej listy
            var pendingGuides = await _context.Guides
                .Include(g => g.User)
                .Where(g => !g.IsApproved && !g.IsDeleted && !g.IsDraft && !g.IsRejected)
                .OrderBy(g => g.CreatedAt)
                .ToListAsync();

            model.PendingGuides = pendingGuides;

            // 3. POSTY USUNIĘTE (Kosz)
            var deletedGuides = await _context.Guides
                .Include(g => g.User)
                .Where(g => g.IsDeleted)
                .OrderBy(g => g.DeletedAt)
                .ToListAsync();

            model.DeletedGuides = deletedGuides;

            // 4. ZGŁOSZENIA (Tickety)
            var tickets = await _context.Tickets
                .Include(t => t.User)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            model.Tickets = tickets;

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

        // W pliku Controllers/AdminController.cs

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectGuide(int id, string reason) // Dodano parametr reason
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide == null) return NotFound();

            // ZMIANA LOGIKI:
            // Zamiast usuwać (IsDeleted = true), oznaczamy jako Odrzucony (IsRejected = true).
            // Dzięki temu użytkownik zobaczy powód i będzie mógł poprawić poradnik.

            guide.IsRejected = true;       // Ustawiamy flagę odrzucenia
            guide.RejectionReason = reason; // Zapisujemy powód wpisany w modalu
            guide.IsApproved = false;      // Dla pewności

            // Upewniamy się, że NIE jest w koszu (jeśli wcześniej tam trafił przez pomyłkę)
            guide.IsDeleted = false;
            guide.DeletedAt = null;

            _context.Update(guide);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Odrzucono poradnik: {guide.Title} (z powodem: {reason})";

            // Powrót do widoku
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer)) return Redirect(referer);

            return RedirectToAction(nameof(Index));
        }


        // --- ZARZĄDZANIE UŻYTKOWNIKAMI (Bez zmian logicznych, tylko autoryzacja Admin) ---

        [Authorize(Roles = "Admin")]
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
                // 1. Odbieramy uprawnienia systemowe
                await _userManager.RemoveFromRoleAsync(user, "Moderator");

                // 2. NOWOŚĆ: Aktualizujemy pole tekstowe dla kolorów w widoku
                user.Role = "User";
                await _userManager.UpdateAsync(user);

                TempData["Success"] = $"Odebrano uprawnienia Moderatora użytkownikowi {user.UserName}.";
            }
            else
            {
                // 1. Nadajemy uprawnienia systemowe
                await _userManager.AddToRoleAsync(user, "Moderator");

                // 2. NOWOŚĆ: Aktualizujemy pole tekstowe dla kolorów w widoku
                user.Role = "Moderator";
                await _userManager.UpdateAsync(user);

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

        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> RestoreGuide(int id)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide != null)
            {
                guide.IsDeleted = false;
                guide.DeletedAt = null;
                // NIE ZMIENIAMY IsApproved. Jeśli był false (oczekujący), to nadal jest false.

                _context.Update(guide);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Przywrócono poradnik z kosza.";
            }

            // Jeśli wywołane z Details, wróć do Details, jeśli z Panelu - do Panelu
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer)) return Redirect(referer);

            return RedirectToAction("Index");
        }

        // ZMIANA: long id -> int id
        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> DeletePermanently(int id)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide != null)
            {
                _context.Guides.Remove(guide);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Poradnik został usunięty permanentnie.";
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> EmptyTrash()
        {
            // Tutaj musimy sprawdzić, czy masz już pole IsDeleted w modelu Guide,
            // jeśli tak - to zadziała:
            var trashGuides = await _context.Guides.Where(g => g.IsDeleted).ToListAsync();

            if (trashGuides.Any())
            {
                _context.Guides.RemoveRange(trashGuides);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Kosz został opróżniony.";
            }
            return RedirectToAction("Index");
        }

        // 2. Zmiana statusu (np. Zamknięcie ticketa)
        [HttpPost]
        public async Task<IActionResult> UpdateTicketStatus(int id, TicketStatus status)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            ticket.Status = status;
            if (status == TicketStatus.Zamknięte)
            {
                ticket.ClosedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> TicketDetails(int id)
        {
            var ticket = await _context.Tickets
                .Include(t => t.User)
                .Include(t => t.Messages)
                    .ThenInclude(m => m.User)
                // --- DODANE: Pobieranie załączników do wiadomości ---
                .Include(t => t.Messages)
                    .ThenInclude(m => m.Attachments)
                // ----------------------------------------------------
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            // AUTOMATYZACJA: Skoro Admin wszedł, to znaczy że widzi nowe wiadomości
            if (ticket.HasUnreadMessage)
            {
                ticket.HasUnreadMessage = false;
                await _context.SaveChangesAsync();
            }

            return View(ticket);
        }

        // 2. Odpowiedź Admina
        [HttpPost]
        public async Task<IActionResult> AdminReply(int id, string message, List<IFormFile> attachments)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            var currentUser = await _userManager.GetUserAsync(User);

            if (ticket == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(message) || (attachments != null && attachments.Any()))
            {
                var msg = new TicketMessage
                {
                    TicketId = id,
                    UserId = currentUser.Id,
                    Message = message ?? "",
                    CreatedAt = DateTime.Now,
                    IsStaffReply = true
                };
                _context.TicketMessages.Add(msg);
                await _context.SaveChangesAsync();

                if (attachments != null)
                {
                    foreach (var file in attachments)
                    {
                        if (file.Length > 0)
                        {
                            using (var ms = new MemoryStream())
                            {
                                await file.CopyToAsync(ms);
                                var att = new TicketAttachment
                                {
                                    TicketMessageId = msg.Id,
                                    FileName = file.FileName,
                                    ContentType = file.ContentType,
                                    FileContent = ms.ToArray()
                                };
                                _context.TicketAttachments.Add(att);
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                ticket.HasUnreadResponse = true;
                if (ticket.Status == TicketStatus.Oczekujące) ticket.Status = TicketStatus.W_trakcie;

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(TicketDetails), new { id = id });
        }

        [HttpPost]
        public async Task<IActionResult> CloseTicket(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            ticket.Status = TicketStatus.Zamknięte;
            ticket.ClosedAt = DateTime.Now;
            ticket.HasUnreadMessage = false; // Po zamknięciu nie chcemy powiadomień

            await _context.SaveChangesAsync();

            // Wracamy do listy zgłoszeń
            return RedirectToAction(nameof(Index));
        }
    }
}