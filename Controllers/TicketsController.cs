using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa_zesp.Data;
using praca_dyplomowa_zesp.Models;
using praca_dyplomowa_zesp.Models.Modules.Users;

namespace praca_dyplomowa_zesp.Controllers
{
    [Authorize]
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public TicketsController(ApplicationDbContext context, UserManager<User> userManager)
        {
            //przypisanie wstrzyknietych serwisow do pol klasy
            _context = context;
            _userManager = userManager;
        }

        #region Actions

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            //pobranie wszystkich zgloszen uzytkownika posortowanych od najnowszych
            var tickets = await _context.Tickets
                .Where(t => t.UserId == user.Id)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tickets);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Ticket ticket, List<IFormFile> attachments)
        {
            var user = await _userManager.GetUserAsync(User);

            //reczne usuniecie zbednych pol z walidacji modelu
            ModelState.Remove(nameof(Ticket.User));
            ModelState.Remove(nameof(Ticket.UserId));

            //uruchomienie pomocniczej walidacji typow plikow
            ValidateAttachments(attachments);

            if (ModelState.IsValid)
            {
                //inicjalizacja danych startowych dla nowego ticketu
                ticket.UserId = user.Id;
                ticket.CreatedAt = DateTime.Now;
                ticket.Status = TicketStatus.Oczekujące;
                ticket.HasUnreadMessage = true;

                _context.Add(ticket);
                await _context.SaveChangesAsync();

                if (attachments != null && attachments.Any())
                {
                    //stworzenie powiazanej wiadomosci w celu przypisania do niej zalacznikow
                    var initialMessage = new TicketMessage
                    {
                        TicketId = ticket.Id,
                        UserId = user.Id,
                        Message = "Załączniki dodane przy utworzeniu zgłoszenia.",
                        CreatedAt = DateTime.Now,
                        IsStaffReply = false
                    };

                    _context.TicketMessages.Add(initialMessage);
                    await _context.SaveChangesAsync();

                    //procesowanie i zapis plikow binarnych w bazie
                    await ProcessAndSaveAttachments(initialMessage.Id, attachments);
                }

                TempData["Success"] = "Zgłoszenie zostało wysłane pomyślnie!";
                return RedirectToAction(nameof(Index));
            }

            return View(ticket);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            //pobranie zgloszenia z pelnym drzewem zaleznosci i wiadomosciami
            var ticket = await _context.Tickets
                .Include(t => t.User)
                .Include(t => t.Messages)
                    .ThenInclude(m => m.User)
                .Include(t => t.Messages)
                    .ThenInclude(m => m.Attachments)
                .FirstOrDefaultAsync(m => m.Id == id);

            //zabezpieczenie przed proby odczytu cudzych zgloszen
            if (ticket == null || ticket.UserId != user.Id)
            {
                return NotFound();
            }

            if (ticket.HasUnreadResponse)
            {
                //reset flagi nowej wiadomosci po wejsciu uzytkownika w detale
                ticket.HasUnreadResponse = false;
                await _context.SaveChangesAsync();
            }

            return View(ticket);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(int id, string message, List<IFormFile> attachments)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            var user = await _userManager.GetUserAsync(User);

            if (ticket == null || ticket.UserId != user.Id) return NotFound();
            //blokada mozliwosci dopisywania wiadomosci do zgloszen oznaczonych jako zamkniete
            if (ticket.Status == TicketStatus.Zamknięte) return RedirectToAction(nameof(Details), new { id });

            if (!string.IsNullOrWhiteSpace(message) || (attachments != null && attachments.Any()))
            {
                var newMessage = new TicketMessage
                {
                    TicketId = id,
                    UserId = user.Id,
                    Message = message ?? string.Empty,
                    CreatedAt = DateTime.Now,
                    IsStaffReply = false
                };

                _context.TicketMessages.Add(newMessage);
                await _context.SaveChangesAsync();

                if (attachments != null && attachments.Any())
                {
                    await ProcessAndSaveAttachments(newMessage.Id, attachments);
                }

                //ustawienie powiadomienia dla personelu o nowej tresci od uzytkownika
                ticket.HasUnreadMessage = true;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        #endregion

        #region Private Helpers

        private void ValidateAttachments(List<IFormFile> attachments)
        {
            if (attachments == null || !attachments.Any()) return;

            //definicja wspieranych formatow graficznych
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            foreach (var file in attachments)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
                {
                    //dodanie bledu do stanu modelu jesli wykryto niebezpieczny plik
                    ModelState.AddModelError("attachments",
                        $"Plik {file.FileName} ma niedozwolone rozszerzenie. Dozwolone formaty: jpg, png, gif.");
                }
            }
        }

        private async Task ProcessAndSaveAttachments(int messageId, List<IFormFile> files)
        {
            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    using var memoryStream = new MemoryStream();
                    //zapisanie strumienia pliku do tablicy bajtow
                    await file.CopyToAsync(memoryStream);

                    var attachment = new TicketAttachment
                    {
                        TicketMessageId = messageId,
                        FileName = file.FileName,
                        ContentType = file.ContentType,
                        FileContent = memoryStream.ToArray()
                    };

                    _context.TicketAttachments.Add(attachment);
                }
            }
            await _context.SaveChangesAsync();
        }

        #endregion
    }
}