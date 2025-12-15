using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa.Data;
using praca_dyplomowa_zesp.Models;
using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Controllers
{
    [Authorize] // Tylko zalogowani mogą pisać tickety
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public TicketsController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Lista zgłoszeń użytkownika
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var tickets = await _context.Tickets
                .Where(t => t.UserId == user.Id)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tickets);
        }

        // Formularz tworzenia (GET)
        public IActionResult Create()
        {
            return View();
        }

        // Wysyłanie zgłoszenia (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Ticket ticket, List<IFormFile> attachments) // Zmiana na List
        {
            var user = await _userManager.GetUserAsync(User);
            ModelState.Remove("User");
            ModelState.Remove("UserId");

            if (ModelState.IsValid)
            {
                ticket.UserId = user.Id;
                ticket.CreatedAt = DateTime.Now;
                ticket.Status = TicketStatus.Oczekujące;
                ticket.HasUnreadMessage = true;

                _context.Add(ticket);
                await _context.SaveChangesAsync();

                // Obsługa wielu załączników
                if (attachments != null && attachments.Any())
                {
                    var msg = new TicketMessage
                    {
                        TicketId = ticket.Id,
                        UserId = user.Id,
                        Message = "Załączniki dodane przy utworzeniu zgłoszenia.",
                        CreatedAt = DateTime.Now,
                        IsStaffReply = false
                    };
                    _context.TicketMessages.Add(msg);
                    await _context.SaveChangesAsync(); // Zapisz żeby mieć msg.Id

                    foreach (var file in attachments)
                    {
                        if (file.Length > 0)
                        {
                            using (var ms = new MemoryStream())
                            {
                                await file.CopyToAsync(ms);
                                var attachment = new TicketAttachment
                                {
                                    TicketMessageId = msg.Id,
                                    FileName = file.FileName,
                                    ContentType = file.ContentType,
                                    FileContent = ms.ToArray()
                                };
                                _context.TicketAttachments.Add(attachment);
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "Zgłoszenie zostało wysłane pomyślnie!";
                return RedirectToAction(nameof(Index));
            }
            return View(ticket);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);

            var ticket = await _context.Tickets
                .Include(t => t.User)
                .Include(t => t.Messages)
                    .ThenInclude(m => m.User) // Żeby widzieć kto napisał wiadomość
                .Include(t => t.Messages)
                    .ThenInclude(m => m.Attachments)
                .FirstOrDefaultAsync(m => m.Id == id);

            // Zabezpieczenie: user widzi tylko swoje tickety (chyba że jest adminem, ale tu jest kontroler usera)
            if (ticket == null || ticket.UserId != user.Id) return NotFound();

            if (ticket.HasUnreadResponse)
            {
                ticket.HasUnreadResponse = false;
                await _context.SaveChangesAsync();
            }

            return View(ticket);
        }

        // 2. Wysyłanie odpowiedzi (User)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(int id, string message, List<IFormFile> attachments) // Zmiana na List
        {
            var ticket = await _context.Tickets.FindAsync(id);
            var user = await _userManager.GetUserAsync(User);

            if (ticket == null || ticket.UserId != user.Id) return NotFound();
            if (ticket.Status == TicketStatus.Zamknięte) return RedirectToAction(nameof(Details), new { id = id });

            if (!string.IsNullOrWhiteSpace(message) || (attachments != null && attachments.Any()))
            {
                var msg = new TicketMessage
                {
                    TicketId = id,
                    UserId = user.Id,
                    Message = message ?? "",
                    CreatedAt = DateTime.Now,
                    IsStaffReply = false
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

                ticket.HasUnreadMessage = true;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = id });
        }
    }
}