using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa.Data;
using praca_dyplomowa_zesp.Models;
using praca_dyplomowa_zesp.Models.Users;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace praca_dyplomowa_zesp.Controllers
{
    /// <summary>
    /// Kontroler obsługujący system zgłoszeń (support ticket) od strony użytkownika.
    /// Umożliwia tworzenie zgłoszeń, przeglądanie historii oraz wysyłanie odpowiedzi z załącznikami.
    /// </summary>
    [Authorize]
    public class TicketsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public TicketsController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        #region Actions

        /// <summary>
        /// Wyświetla listę wszystkich zgłoszeń należących do zalogowanego użytkownika.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var tickets = await _context.Tickets
                .Where(t => t.UserId == user.Id)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return View(tickets);
        }

        /// <summary>
        /// Wyświetla formularz tworzenia nowego zgłoszenia.
        /// </summary>
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        /// <summary>
        /// Przetwarza formularz nowego zgłoszenia wraz z opcjonalnymi załącznikami graficznymi.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Ticket ticket, List<IFormFile> attachments)
        {
            var user = await _userManager.GetUserAsync(User);

            // Usunięcie walidacji dla pól przypisywanych ręcznie w kontrolerze
            ModelState.Remove(nameof(Ticket.User));
            ModelState.Remove(nameof(Ticket.UserId));

            ValidateAttachments(attachments);

            if (ModelState.IsValid)
            {
                ticket.UserId = user.Id;
                ticket.CreatedAt = DateTime.Now;
                ticket.Status = TicketStatus.Oczekujące;
                ticket.HasUnreadMessage = true;

                _context.Add(ticket);
                await _context.SaveChangesAsync();

                if (attachments != null && attachments.Any())
                {
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

                    await ProcessAndSaveAttachments(initialMessage.Id, attachments);
                }

                TempData["Success"] = "Zgłoszenie zostało wysłane pomyślnie!";
                return RedirectToAction(nameof(Index));
            }

            return View(ticket);
        }

        /// <summary>
        /// Wyświetla szczegóły konkretnego zgłoszenia wraz z historią wiadomości.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var ticket = await _context.Tickets
                .Include(t => t.User)
                .Include(t => t.Messages)
                    .ThenInclude(m => m.User)
                .Include(t => t.Messages)
                    .ThenInclude(m => m.Attachments)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (ticket == null || ticket.UserId != user.Id)
            {
                return NotFound();
            }

            // Oznaczanie odpowiedzi od personelu jako przeczytanej przez użytkownika
            if (ticket.HasUnreadResponse)
            {
                ticket.HasUnreadResponse = false;
                await _context.SaveChangesAsync();
            }

            return View(ticket);
        }

        /// <summary>
        /// Dodaje nową wiadomość użytkownika do istniejącego zgłoszenia.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reply(int id, string message, List<IFormFile> attachments)
        {
            var ticket = await _context.Tickets.FindAsync(id);
            var user = await _userManager.GetUserAsync(User);

            if (ticket == null || ticket.UserId != user.Id) return NotFound();
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

                ticket.HasUnreadMessage = true;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Sprawdza, czy przesłane załączniki mają dozwolone rozszerzenia graficzne.
        /// </summary>
        private void ValidateAttachments(List<IFormFile> attachments)
        {
            if (attachments == null || !attachments.Any()) return;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            foreach (var file in attachments)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError("attachments",
                        $"Plik {file.FileName} ma niedozwolone rozszerzenie. Dozwolone formaty: jpg, png, gif.");
                }
            }
        }

        /// <summary>
        /// Konwertuje pliki przesłane przez formularz na format binarny i zapisuje je w bazie danych.
        /// </summary>
        private async Task ProcessAndSaveAttachments(int messageId, List<IFormFile> files)
        {
            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    using var memoryStream = new MemoryStream();
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