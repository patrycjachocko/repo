using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa_zesp.Data;
using praca_dyplomowa_zesp.Models;
using praca_dyplomowa_zesp.Models.Modules.Guides;
using praca_dyplomowa_zesp.Models.Modules.Users;
using praca_dyplomowa_zesp.Models.ViewModels.Admin;

namespace praca_dyplomowa_zesp.Controllers
{
    [Authorize(Roles = "Admin,Moderator")]
    public class AdminController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public AdminController(UserManager<User> userManager, ApplicationDbContext context)
        {
            //inicjalizacja wstrzyknietych serwisow do pol klasy
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index(string searchString)
        {
            //przygotowanie pustego modelu panelu administracyjnego
            var model = new AdminPanelViewModel
            {
                SearchString = searchString,
                Users = new List<AdminUserDto>(),
                PendingGuides = new List<Guide>(),
                DeletedGuides = new List<Guide>(),
                Tickets = new List<Ticket>()
            };

            if (User.IsInRole("Admin"))
            {
                var usersQuery = _userManager.Users.AsQueryable();

                if (!string.IsNullOrEmpty(searchString))
                {
                    //filtrowanie listy uzytkownikow po nazwie lub adresie email
                    usersQuery = usersQuery.Where(u => u.UserName.Contains(searchString) || u.Email.Contains(searchString));
                }

                var usersList = await usersQuery.ToListAsync();

                foreach (var user in usersList)
                {
                    //mapowanie danych uzytkownika na obiekt dto z uwzglednieniem rol i blokad
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

            //pobieranie poradników oczekujacych na zatwierdzenie przez moderacje
            model.PendingGuides = await _context.Guides
                .Include(g => g.User)
                .Where(g => !g.IsApproved && !g.IsDeleted && !g.IsDraft && !g.IsRejected)
                .OrderBy(g => g.CreatedAt)
                .ToListAsync();

            //pobieranie elementow oznaczonych jako usuniete (kosz)
            model.DeletedGuides = await _context.Guides
                .Include(g => g.User)
                .Where(g => g.IsDeleted)
                .OrderBy(g => g.DeletedAt)
                .ToListAsync();

            //pobranie wszystkich zgloszen technicznych od najnowszych
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
            //blokada modyfikacji wlasnego konta oraz glownego konta admin
            if (user == null || user.UserName == "Admin" || user.UserName == User.Identity.Name)
            {
                TempData["Error"] = "Nie można zmienić uprawnień tego użytkownika.";
                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.IsInRoleAsync(user, "Moderator"))
            {
                //odebranie uprawnien moderatora i przywrocenie roli zwyklego uzytkownika
                await _userManager.RemoveFromRoleAsync(user, "Moderator");
                user.Role = "User";
                TempData["Success"] = $"Odebrano uprawnienia Moderatora użytkownikowi {user.UserName}.";
            }
            else
            {
                //nadanie uprawnien moderatorskich
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
                //nakladanie blokady czasowej logowania przy uzyciu mechanizmu identity lockout
                var banUntil = DateTimeOffset.Now.AddDays(days);
                await _userManager.SetLockoutEndDateAsync(user, banUntil);
                await _userManager.SetLockoutEnabledAsync(user, true);
                user.BanReason = reason;
                TempData["Success"] = $"Użytkownik {user.UserName} został zbanowany na {days} dni.";
            }
            else
            {
                //zdjecie blokady logowania
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
                //ustawienie flagi blokujacej mozliwosc publikowania tresci
                user.BanEnd = DateTimeOffset.Now.AddHours(hours);
                user.isBanned = true;
                TempData["Success"] = $"Użytkownik {user.UserName} został wyciszony na {hours} godzin.";
            }
            else
            {
                //odblokowanie interakcji uzytkownika
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

            //zatwierdzenie poradnika sprawia ze staje sie widoczny publicznie
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

            //oznaczenie poradnika jako odrzucony wraz z zapisem powodu dla autora
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
                //cofniecie miekkiego usuniecia i przywrocenie elementu z kosza
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
                //calkowite usuniecie rekordu z bazy danych
                _context.Guides.Remove(guide);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Poradnik został usunięty permanentnie.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> EmptyTrash()
        {
            //zbiorowe usuwanie wszystkich poradnikow znajdujacych sie w koszu
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
            //pobranie zgloszenia wraz z pelna historia wiadomosci i zalacznikami
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
                //resetowanie powiadomienia o nowej wiadomosci po wejsciu w detale
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
                //tworzenie nowej wiadomosci od personelu pomocniczego
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
                    //zapisywanie plikow przeslanych w odpowiedzi admina
                    await ProcessTicketAttachments(msg.Id, attachments);
                }

                //zmiana statusu zgloszenia na aktywne po udzieleniu odpowiedzi
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

            //reczna aktualizacja statusu zgloszenia przez moderatora
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

            //szybkie zamkniecie zgloszenia i odnotowanie czasu zakonczenia
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
            //konwersja obrazu binarnego na format base64 do wyswietlenia w przegladarce
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
                        //przetwarzanie pliku na tablice bajtow do zapisu w bazie
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
            //pobranie adresu poprzedniej strony z naglowkow zapytania http
            var referer = Request.Headers["Referer"].ToString();
            return !string.IsNullOrEmpty(referer) ? Redirect(referer) : RedirectToAction(nameof(Index));
        }

        #endregion
    }
}