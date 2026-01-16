using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa.Data;
using praca_dyplomowa_zesp.Models;
using praca_dyplomowa_zesp.Models.Modules.Admin;
using praca_dyplomowa_zesp.Models.Modules.Guides;
using praca_dyplomowa_zesp.Models.Users;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace praca_dyplomowa_zesp.Controllers
{
    /// <summary>
    /// Kontroler administracyjny zarządzający użytkownikami, treściami (poradnikami) 
    /// oraz systemem zgłoszeń technicznych (ticketów).
    /// </summary>
    [Authorize(Roles = "Admin,Moderator")]
    public class AdminController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminController(UserManager<User> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        /// <summary>
        /// Wyświetla główny panel administracyjny z podziałem na sekcje zależne od uprawnień.
        /// </summary>
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

            // Sekcja zarządzania użytkownikami - dostępna tylko dla Administratora
            if (User.IsInRole("Admin"))
            {
                var usersQuery = _userManager.Users.AsQueryable();

                if (!string.IsNullOrEmpty(searchString))
                {
                    usersQuery = usersQuery.Where(u => u.UserName.Contains(searchString) || u.Email.Contains(searchString));
                }

                var usersList = await usersQuery.ToListAsync();

                foreach (var user in usersList)
                {
                    model.Users.Add(new AdminUserDto
                    {
                        Id = user.Id,
                        UserName = user.UserName,
                        Login = user.Login,
                        Roles = (await _userManager.GetRolesAsync(user)).ToList(),
                        IsLockedOut = await _userManager.IsLockedOutAsync(user),
                        LockoutEnd = user.LockoutEnd,
                        IsMuted = user.BanEnd.HasValue && user.BanEnd.Value > DateTimeOffset.Now,
                        MuteEnd = user.BanEnd,
                        AvatarUrl = GetUserAvatar(user)
                    });
                }
            }

            // Pobieranie danych dla Moderatora i Administratora
            model.PendingGuides = await _context.Guides
                .Include(g => g.User)
                .Where(g => !g.IsApproved && !g.IsDeleted && !g.IsDraft && !g.IsRejected)
                .OrderBy(g => g.CreatedAt)
                .ToListAsync();

            model.DeletedGuides = await _context.Guides
                .Include(g => g.User)
                .Where(g => g.IsDeleted)
                .OrderBy(g => g.DeletedAt)
                .ToListAsync();

            model.Tickets = await _context.Tickets
                .Include(t => t.User)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(model);
        }

        #region User Management (Admin Only)

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleModerator(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || user.UserName == "Admin" || user.UserName == User.Identity.Name)
            {
                TempData["Error"] = "Nie można zmienić uprawnień tego użytkownika.";
                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.IsInRoleAsync(user, "Moderator"))
            {
                await _userManager.RemoveFromRoleAsync(user, "Moderator");
                user.Role = "User";
                TempData["Success"] = $"Odebrano uprawnienia Moderatora użytkownikowi {user.UserName}.";
            }
            else
            {
                await _userManager.AddToRoleAsync(user, "Moderator");
                user.Role = "Moderator";
                TempData["Success"] = $"Nadano uprawnienia Moderatora użytkownikowi {user.UserName}.";
            }

            await _userManager.UpdateAsync(user);
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BanUser(Guid userId, int days, string reason)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || user.UserName == "Admin" || user.UserName == User.Identity.Name)
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
                TempData["Success"] = $"Użytkownik {user.UserName} został zbanowany na {days} dni.";
            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                user.BanReason = null;
                TempData["Success"] = $"Użytkownik {user.UserName} został odbanowany.";
            }

            await _userManager.UpdateAsync(user);
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
                user.isBanned = true;
                TempData["Success"] = $"Użytkownik {user.UserName} został wyciszony na {hours} godzin.";
            }
            else
            {
                user.BanEnd = null;
                user.isBanned = false;
                TempData["Success"] = $"Użytkownik {user.UserName} został odciszony.";
            }

            await _userManager.UpdateAsync(user);
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Content Moderation (Guides)

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
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectGuide(int id, string reason)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide == null) return NotFound();

            guide.IsRejected = true;
            guide.RejectionReason = reason;
            guide.IsApproved = false;
            guide.IsDeleted = false;
            guide.DeletedAt = null;

            _context.Update(guide);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Odrzucono poradnik: {guide.Title} (Powód: {reason})";
            return RedirectToReferer();
        }

        [HttpPost]
        public async Task<IActionResult> RestoreGuide(int id)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide != null)
            {
                guide.IsDeleted = false;
                guide.DeletedAt = null;
                _context.Update(guide);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Przywrócono poradnik z kosza.";
            }

            return RedirectToReferer();
        }

        [HttpPost]
        public async Task<IActionResult> DeletePermanently(int id)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide != null)
            {
                _context.Guides.Remove(guide);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Poradnik został usunięty permanentnie.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> EmptyTrash()
        {
            var trashGuides = await _context.Guides.Where(g => g.IsDeleted).ToListAsync();
            if (trashGuides.Any())
            {
                _context.Guides.RemoveRange(trashGuides);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Kosz został opróżniony.";
            }
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Ticket System (Support)

        [HttpGet]
        public async Task<IActionResult> TicketDetails(int id)
        {
            var ticket = await _context.Tickets
                .Include(t => t.User)
                .Include(t => t.Messages)
                    .ThenInclude(m => m.User)
                .Include(t => t.Messages)
                    .ThenInclude(m => m.Attachments)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            if (ticket.HasUnreadMessage)
            {
                ticket.HasUnreadMessage = false;
                await _context.SaveChangesAsync();
            }

            return View(ticket);
        }

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

                if (attachments != null && attachments.Any())
                {
                    await ProcessTicketAttachments(msg.Id, attachments);
                }

                ticket.HasUnreadResponse = true;
                if (ticket.Status == TicketStatus.Oczekujące) ticket.Status = TicketStatus.W_trakcie;

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(TicketDetails), new { id = id });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTicketStatus(int id, TicketStatus status)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            ticket.Status = status;
            if (status == TicketStatus.Zamknięte) ticket.ClosedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CloseTicket(int id)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            if (ticket == null) return NotFound();

            ticket.Status = TicketStatus.Zamknięte;
            ticket.ClosedAt = DateTime.Now;
            ticket.HasUnreadMessage = false;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        #endregion

        #region Helpers

        private string GetUserAvatar(User user)
        {
            return user.ProfilePicture != null
                ? $"data:{user.ProfilePictureContentType};base64,{Convert.ToBase64String(user.ProfilePicture)}"
                : "/uploads/avatars/default_avatar.png";
        }

        private async Task ProcessTicketAttachments(int messageId, List<IFormFile> attachments)
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
                            TicketMessageId = messageId,
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

        private IActionResult RedirectToReferer()
        {
            var referer = Request.Headers["Referer"].ToString();
            return !string.IsNullOrEmpty(referer) ? Redirect(referer) : RedirectToAction(nameof(Index));
        }

        #endregion
    }
}