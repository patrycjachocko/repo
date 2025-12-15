using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using praca_dyplomowa_zesp.Models.API;
using Microsoft.AspNetCore.Identity;
using praca_dyplomowa_zesp.Models.Users;
using Microsoft.AspNetCore.Authorization;
using praca_dyplomowa_zesp.Models.Modules.Guides;
using System.IO;
using praca_dyplomowa_zesp.Models.Interactions.Rates;
using praca_dyplomowa_zesp.Models.Interactions.Comments;
using praca_dyplomowa_zesp.Models.Interactions.Comments.Replies;
using praca_dyplomowa_zesp.Models.Interactions.Reactions;
using Rotativa.AspNetCore;

namespace praca_dyplomowa_zesp.Controllers
{
    [AllowAnonymous]
    public class GuidesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IGDBClient _igdbClient;
        private readonly UserManager<User> _userManager;

        public GuidesController(ApplicationDbContext context, IGDBClient igdbClient, UserManager<User> userManager)
        {
            _context = context;
            _igdbClient = igdbClient;
            _userManager = userManager;
        }

        private Guid GetCurrentUserId()
        {
            var userIdString = _userManager.GetUserId(User);
            if (Guid.TryParse(userIdString, out Guid userId)) return userId;
            return Guid.Empty;
        }

        private bool IsUserMuted(User user)
        {
            // Jeśli BanEnd ma wartość i ta data jest w przyszłości -> użytkownik jest wyciszony
            return user.BanEnd.HasValue && user.BanEnd.Value > DateTimeOffset.Now;
        }

        // GET: Guides/Index/123
        public async Task<IActionResult> Index(long gameId, string searchString, string sortOrder)
        {
            if (gameId <= 0) return NotFound();

            // Przekazanie parametrów do widoku
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;

            // --- LOGIKA IGDB ---
            var gameQuery = $"fields name, parent_game, version_parent, category, collections.id; where id = {gameId}; limit 1;";
            var gameJsonResponse = await _igdbClient.ApiRequestAsync("games", gameQuery);
            var gameDetailsFromApi = (JsonConvert.DeserializeObject<List<ApiGame>>(gameJsonResponse) ?? new List<ApiGame>()).FirstOrDefault();

            if (gameDetailsFromApi == null) return NotFound();

            if (gameDetailsFromApi.Parent_game.HasValue)
                return RedirectToAction("Index", new { gameId = gameDetailsFromApi.Parent_game.Value });
            if (gameDetailsFromApi.Version_parent.HasValue)
                return RedirectToAction("Index", new { gameId = gameDetailsFromApi.Version_parent.Value });

            if (gameDetailsFromApi.Category != 0 && gameDetailsFromApi.Collections != null && gameDetailsFromApi.Collections.Any())
            {
                var collectionId = gameDetailsFromApi.Collections.First().Id;
                var collectionQuery = $"fields id, name; where collections = ({collectionId}) & category = 0; limit 50;";
                var collectionJson = await _igdbClient.ApiRequestAsync("games", collectionQuery);
                if (!string.IsNullOrEmpty(collectionJson))
                {
                    var mainGames = JsonConvert.DeserializeObject<List<ApiGame>>(collectionJson);
                    var matched = mainGames?.OrderByDescending(g => g.Name.Length).FirstOrDefault(g => gameDetailsFromApi.Name.Contains(g.Name));
                    if (matched != null && matched.Id != gameId) return RedirectToAction("Index", new { gameId = matched.Id });
                }
            }
            // -------------------------------------

            bool isInLibrary = false;
            var currentUserId = Guid.Empty;
            bool canSeeAllPending = false;

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                currentUserId = GetCurrentUserId();
                if (currentUserId != Guid.Empty)
                {
                    isInLibrary = await _context.GamesInLibraries.AnyAsync(g => g.UserId == currentUserId && g.IgdbGameId == gameId);
                }

                // Sprawdzenie uprawnień do widzenia wszystkich oczekujących
                canSeeAllPending = User.IsInRole("Admin") || User.IsInRole("Moderator");
            }

            // --- BUDOWANIE ZAPYTANIA ---
            var guidesQuery = _context.Guides
                .Include(g => g.User)
                .Include(g => g.Rates)
                .Where(g => g.IgdbGameId == gameId)
                // ZMIANA: Najpierw wykluczamy wszystkie usunięte z tego widoku (User ich nie widzi, Admin ma od tego Panel Admina)
                .Where(g => g.IsDeleted == false)
                .Where(g =>
                    // 1. PUBLICZNE: Musi być zatwierdzony
                    (g.IsApproved == true) ||
                    // 2. AUTOR: Widzi swoje (nawet jeśli oczekują na akceptację, ale NIE usunięte - patrz wyżej)
                    (g.UserId == currentUserId) ||
                    // 3. WŁADZA: Widzi wszystkie oczekujące
                    (canSeeAllPending == true)
                )
                .AsQueryable();

            // 1. Wyszukiwanie
            if (!string.IsNullOrEmpty(searchString))
            {
                var term = searchString.ToLower();
                guidesQuery = guidesQuery.Where(s =>
                    s.Title.ToLower().Contains(term) ||
                    s.Content.ToLower().Contains(term));
            }

            // 2. POBRANIE DANYCH DO PAMIĘCI
            var guidesFromDb = await guidesQuery.ToListAsync();

            // 3. SORTOWANIE W PAMIĘCI
            switch (sortOrder)
            {
                case "rating_desc":
                    guidesFromDb = guidesFromDb
                        .OrderByDescending(g => g.Rates.Any() ? g.Rates.Average(r => r.Value) : 0)
                        .ToList();
                    break;
                case "newest":
                default:
                    guidesFromDb = guidesFromDb
                        .OrderByDescending(g => g.CreatedAt)
                        .ToList();
                    break;
            }

            var viewModel = new GuidesViewModel
            {
                IgdbGameId = gameId,
                GameName = gameDetailsFromApi.Name ?? "Brak nazwy",
                IsInLibrary = isInLibrary,
                Guides = guidesFromDb
            };

            return View(viewModel);
        }

        // GET: Guides/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var guide = await _context.Guides
                .Include(g => g.User)
                .Include(g => g.Rates) // Do obliczenia średniej
                .FirstOrDefaultAsync(m => m.Id == id);

            if (guide == null) return NotFound();

            // Pobieranie komentarzy z odpowiedziami i reakcjami
            var comments = await _context.Comments
                .Where(c => c.GuideId == id)
                .Include(c => c.Author)
                .Include(c => c.Reactions) // Reakcje dla głównego komentarza
                .Include(c => c.Replies)
                    .ThenInclude(r => r.Author) // Autor odpowiedzi
                                                // --- BRAKOWAŁO TEGO FRAGMENTU: ---
                .Include(c => c.Replies)
                    .ThenInclude(r => r.Reactions) // <--- TO JEST KLUCZOWE: Reakcje dla odpowiedzi
                                                   // ---------------------------------
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var viewModel = new GuideDetailsViewModel
            {
                Guide = guide,
                Comments = comments,
                UserRating = 0,
                AverageRating = guide.Rates.Any() ? guide.Rates.Average(r => r.Value) : 0
            };

            // Sprawdzenie czy user ocenił
            if (User.Identity.IsAuthenticated)
            {
                var userId = GetCurrentUserId();
                var existingRate = await _context.Rates
                    .FirstOrDefaultAsync(r => r.GuideId == id && r.UserId == userId);

                if (existingRate != null) viewModel.UserRating = existingRate.Value;
            }

            if (guide.IsDeleted && !User.IsInRole("Admin") && !User.IsInRole("Moderator"))
            {
                return NotFound();
            }

            return View(viewModel);
        }

        // --- AKCJE DLA OCEN I KOMENTARZY ---

        // 1. Podmień metodę RateGuide na tę wersję:
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RateGuide([FromBody] RateRequest request)
        {
            // Zabezpieczenie danych
            if (request == null) return BadRequest();

            double ratingValue = request.RatingValue;

            // Logika ograniczeń (1-5, zaokrąglanie do połówek)
            if (ratingValue < 1) ratingValue = 1;
            if (ratingValue > 5) ratingValue = 5;
            ratingValue = Math.Round(ratingValue * 2, MidpointRounding.AwayFromZero) / 2;

            var user = await _userManager.GetUserAsync(User);
            var guide = await _context.Guides.Include(g => g.Rates).FirstOrDefaultAsync(g => g.Id == request.GuideId);

            if (guide == null) return NotFound();

            var existingRate = await _context.Rates
                .FirstOrDefaultAsync(r => r.GuideId == request.GuideId && r.UserId == user.Id);

            if (existingRate != null)
            {
                existingRate.Value = ratingValue;
                _context.Rates.Update(existingRate);
            }
            else
            {
                var rate = new Rate
                {
                    UserId = user.Id,
                    GuideId = request.GuideId,
                    Value = ratingValue,
                    CreatedAt = DateTime.Now
                };
                _context.Rates.Add(rate);
            }

            await _context.SaveChangesAsync();

            // Oblicz nową średnią, żeby zaktualizować ją na ekranie bez odświeżania
            var newAverage = guide.Rates.Any() ? guide.Rates.Average(r => r.Value) : ratingValue;

            return Ok(new { success = true, message = "Ocena zapisana!", newAverage = newAverage });
        }

        public class RateRequest
        {
            public int GuideId { get; set; }
            public double RatingValue { get; set; }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int guideId, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return RedirectToAction("Details", new { id = guideId });

            var user = await _userManager.GetUserAsync(User);

            // ZMIANA: Sprawdzenie Mute przy komentarzach
            if (IsUserMuted(user))
            {
                TempData["Error"] = $"Jesteś wyciszony do {user.BanEnd?.ToString("dd.MM.yyyy HH:mm")}. Nie możesz dodawać komentarzy.";
                return RedirectToAction("Details", new { id = guideId });
            }

            var comment = new Comment
            {
                GuideId = guideId,
                UserId = user.Id,
                Content = content,
                CreatedAt = DateTime.Now
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = guideId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReply(Guid commentId, int guideId, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return RedirectToAction("Details", new { id = guideId });

            var user = await _userManager.GetUserAsync(User);

            // ZMIANA: Sprawdzenie Mute przy odpowiedziach
            if (IsUserMuted(user))
            {
                TempData["Error"] = $"Jesteś wyciszony do {user.BanEnd?.ToString("dd.MM.yyyy HH:mm")}. Nie możesz odpowiadać.";
                return RedirectToAction("Details", new { id = guideId });
            }

            var reply = new Reply
            {
                ParentCommentId = commentId,
                UserId = user.Id,
                Content = content,
                CreatedAt = DateTime.Now
            };

            _context.Add(reply);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = guideId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleReaction(Guid? commentId, Guid? replyId, int guideId, bool isUpvote)
        {
            var user = await _userManager.GetUserAsync(User);

            // Sprawdzenie czy użytkownik nie ma mute
            if (IsUserMuted(user)) return Forbid(); // Lub przekierowanie z komunikatem

            Reaction existingReaction = null;

            // 1. Sprawdzamy, czy to reakcja na KOMENTARZ
            if (commentId.HasValue)
            {
                existingReaction = await _context.Reactions
                    .FirstOrDefaultAsync(r => r.CommentId == commentId.Value && r.UserId == user.Id);
            }
            // 2. Sprawdzamy, czy to reakcja na ODPOWIEDŹ
            else if (replyId.HasValue)
            {
                existingReaction = await _context.Reactions
                    .FirstOrDefaultAsync(r => r.ReplyId == replyId.Value && r.UserId == user.Id);
            }
            else
            {
                return BadRequest("Brak ID komentarza lub odpowiedzi.");
            }

            var targetType = isUpvote ? ReactionType.Like : ReactionType.Dislike;

            if (existingReaction != null)
            {
                // Jeśli użytkownik klika to samo co już ma -> usuwamy (toggle off)
                if (existingReaction.Type == targetType)
                {
                    _context.Reactions.Remove(existingReaction);
                }
                // Jeśli zmienia zdanie (z like na dislike lub odwrotnie) -> aktualizujemy
                else
                {
                    existingReaction.Type = targetType;
                    _context.Reactions.Update(existingReaction);
                }
            }
            else
            {
                // Nowa reakcja
                var reaction = new Reaction
                {
                    UserId = user.Id,
                    Type = targetType,
                    // Ważne: Ustawiamy jedno, drugie zostaje null
                    CommentId = commentId,
                    ReplyId = replyId
                };
                _context.Reactions.Add(reaction);
            }

            await _context.SaveChangesAsync();

            // Zachowujemy pozycję na stronie (kotwica)
            return RedirectToAction("Details", new { id = guideId, section = "comments" });
        }

        // --- POZOSTAŁE METODY (Create, Edit, Delete) - BEZ ZMIAN ---

        // GET: Create
        [Authorize]
        public async Task<IActionResult> Create(long gameId)
        {
            if (gameId <= 0) return NotFound();

            // ZMIANA: Sprawdzenie Mute przed wejściem na formularz
            var user = await _userManager.GetUserAsync(User);
            if (IsUserMuted(user))
            {
                TempData["Error"] = $"Twoje konto jest wyciszone do {user.BanEnd?.ToString("dd.MM.yyyy HH:mm")}. Nie możesz tworzyć nowych treści.";
                // Możesz przekierować do Index gry albo na Profil
                return RedirectToAction("Index", new { gameId = gameId });
            }

            return View(new Guide { IgdbGameId = gameId });
        }

        // POST: Create
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guide guide, IFormFile? coverImageFile)
        {
            // Musimy pobrać usera od razu, żeby sprawdzić mute
            var user = await _userManager.GetUserAsync(User);

            // ZMIANA: Sprawdzenie Mute przy wysyłaniu formularza
            if (IsUserMuted(user))
            {
                ModelState.AddModelError(string.Empty, $"Twoje konto jest wyciszone do {user.BanEnd?.ToString("dd.MM.yyyy HH:mm")}. Nie możesz opublikować poradnika.");
                // Jeśli zablokujemy tutaj, musimy pamiętać o ponownym wyświetleniu widoku
                return View(guide);
            }
            // Usuwamy walidację dla pól, które uzupełniamy automatycznie
            ModelState.Remove("User");
            ModelState.Remove("CoverImage");

            // --- NOWY WARUNEK: Sprawdzenie czy plik istnieje ---
            if (coverImageFile == null || coverImageFile.Length == 0)
            {
                // Jeśli brak pliku, dodajemy błąd do modelu.
                // Klucz "CoverImage" posłuży do wyświetlenia komunikatu w widoku.
                ModelState.AddModelError("CoverImage", "Okładka poradnika jest wymagana!");
            }

            if (ModelState.IsValid)
            {
                guide.UserId = GetCurrentUserId();
                guide.CreatedAt = DateTime.Now;

                if (User.IsInRole("Admin") || User.IsInRole("Moderator"))
                {
                    guide.IsApproved = true; // Admin/Mod publikuje od razu
                }
                else
                {
                    guide.IsApproved = false; // Zwykły user czeka na akceptację
                }

                // Tutaj już wiemy, że plik istnieje, bo przeszliśmy walidację wyżej
                using (var memoryStream = new MemoryStream())
                {
                    await coverImageFile.CopyToAsync(memoryStream);
                    guide.CoverImage = memoryStream.ToArray();
                    guide.CoverImageContentType = coverImageFile.ContentType;
                }

                _context.Add(guide);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { gameId = guide.IgdbGameId });
            }

            // Jeśli coś poszło nie tak (np. brak pliku), wracamy do formularza
            return View(guide);
        }

        // GET: Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var guide = await _context.Guides.FindAsync(id);
            if (guide == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            if (guide.UserId != currentUserId && !User.IsInRole("Admin") && !User.IsInRole("Moderator"))
            {
                return Forbid();
            }

            return View(guide);
        }

        // POST: Edit/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Guide guide, IFormFile? coverImageFile)
        {
            if (id != guide.Id) return NotFound();

            // Usuwamy walidację pól, które uzupełniamy ręcznie z bazy
            ModelState.Remove("User");
            ModelState.Remove("CoverImage");

            if (ModelState.IsValid)
            {
                try
                {
                    // 1. Pobieramy ORYGINAŁ z bazy danych (AsNoTracking, żeby nie blokować kontekstu)
                    var existingGuide = await _context.Guides.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);

                    if (existingGuide == null) return NotFound();

                    // 2. Sprawdzamy uprawnienia: Autor, Admin lub Moderator
                    var currentUserId = GetCurrentUserId();
                    bool isAuthorized = existingGuide.UserId == currentUserId || User.IsInRole("Admin") || User.IsInRole("Moderator");

                    if (!isAuthorized) return Forbid();

                    // 3. PRZEPISANIE KLUCZOWYCH DANYCH Z ORYGINAŁU
                    guide.UserId = existingGuide.UserId;       // Autor się nie zmienia
                    guide.CreatedAt = existingGuide.CreatedAt; // Data utworzenia się nie zmienia
                    guide.UpdatedAt = DateTime.Now;            // Data aktualizacji = teraz

                    // --- KLUCZOWA ZMIANA ---
                    // Zachowujemy status taki, jaki był w bazie.
                    // Jeśli był zatwierdzony -> zostaje zatwierdzony.
                    // Jeśli czekał na akceptację -> nadal czeka.
                    guide.IsApproved = existingGuide.IsApproved;
                    guide.IsDeleted = existingGuide.IsDeleted;
                    guide.DeletedAt = existingGuide.DeletedAt;
                    // -----------------------

                    // 4. Obsługa zdjęcia (jeśli wgrano nowe - podmień, jeśli nie - zostaw stare)
                    if (coverImageFile != null && coverImageFile.Length > 0)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await coverImageFile.CopyToAsync(memoryStream);
                            guide.CoverImage = memoryStream.ToArray();
                            guide.CoverImageContentType = coverImageFile.ContentType;
                        }
                    }
                    else
                    {
                        guide.CoverImage = existingGuide.CoverImage;
                        guide.CoverImageContentType = existingGuide.CoverImageContentType;
                    }

                    // 5. Zapis zmian
                    _context.Update(guide);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Guides.Any(e => e.Id == guide.Id)) return NotFound();
                    else throw;
                }

                // Przekierowanie:
                // Jeśli post jest zatwierdzony lub użytkownik to admin/mod -> do widoku szczegółów
                if (guide.IsDeleted)
                {
                    return RedirectToAction("Index", "Admin"); // Wracamy do panelu admina (do kosza)
                }
                if (guide.IsApproved || User.IsInRole("Admin") || User.IsInRole("Moderator"))
                {
                    return RedirectToAction(nameof(Details), new { id = guide.Id });
                }
                else
                {
                    // Jeśli post nadal wisi jako oczekujący (i edytował go zwykły user), wracamy do listy (gdzie zobaczy go jako "pending")
                    return RedirectToAction(nameof(Index), new { gameId = guide.IgdbGameId });
                }
            }
            return View(guide);
        }

        // GET: Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var guide = await _context.Guides
                .Include(g => g.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (guide == null) return NotFound();

            var currentUserId = GetCurrentUserId();
            if (guide.UserId != currentUserId && !User.IsInRole("Admin") && !User.IsInRole("Moderator"))
            {
                return Forbid();
            }

            return View(guide);
        }

        // POST: Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide != null)
            {
                var currentUserId = GetCurrentUserId();
                if (guide.UserId != currentUserId && !User.IsInRole("Admin") && !User.IsInRole("Moderator"))
                {
                    return Forbid();
                }

                long gameId = guide.IgdbGameId;
                guide.IsDeleted = true;
                guide.DeletedAt = DateTime.Now;
                _context.Update(guide);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { gameId = gameId });
            }
            return RedirectToAction(nameof(Index), "Games");
        }

        public async Task<IActionResult> DownloadPdf(int id)
        {
            var guide = await _context.Guides
                .Include(g => g.User)
                .Include(g => g.Rates)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (guide == null) return NotFound();

            // Używamy tego samego modelu co w Details, ale wypełniamy tylko to co potrzebne
            var viewModel = new GuideDetailsViewModel
            {
                Guide = guide,
                AverageRating = guide.Rates.Any() ? guide.Rates.Average(r => r.Value) : 0
                // Komentarze nie są potrzebne w PDF, więc ich nie pobieramy
            };

            // Zwracamy widok "DetailsPdf" jako plik PDF
            // FileName ustala nazwę pliku przy pobieraniu
            return new ViewAsPdf("DetailsPdf", viewModel)
            {
                FileName = $"Poradnik_{guide.Title}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                // Opcjonalnie: marginesy
                PageMargins = new Rotativa.AspNetCore.Options.Margins(10, 10, 10, 10)
            };
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")] // Tylko dla władzy
        public async Task<IActionResult> ApproveGuide(int id)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide == null) return NotFound();

            guide.IsApproved = true;
            _context.Update(guide);
            await _context.SaveChangesAsync();

            // Wróć tam, skąd przyszedłeś (np. do panelu admina)
            return Redirect(Request.Headers["Referer"].ToString());
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditComment(Guid commentId, int guideId, string content)
        {
            var comment = await _context.Comments.FindAsync(commentId);
            if (comment == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (comment.UserId != user.Id) return Forbid(); // Tylko autor może edytować

            if (IsUserMuted(user))
            {
                TempData["Error"] = "Jesteś wyciszony, nie możesz edytować.";
                return RedirectToAction("Details", new { id = guideId });
            }

            comment.Content = content;
            // Opcjonalnie: comment.UpdatedAt = DateTime.Now; (jeśli dodasz takie pole)

            _context.Update(comment);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = guideId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(Guid commentId, int guideId)
        {
            var comment = await _context.Comments.FindAsync(commentId);
            if (comment == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);

            // Logika uprawnień: Autor LUB Admin LUB Moderator
            bool canDelete = comment.UserId == user.Id || User.IsInRole("Admin") || User.IsInRole("Moderator");

            if (!canDelete) return Forbid();

            _context.Comments.Remove(comment); // Kaskadowo usunie odpowiedzi dzięki konfiguracji w DbContext
            await _context.SaveChangesAsync();

            TempData["Success"] = "Komentarz został usunięty.";
            return RedirectToAction("Details", new { id = guideId });
        }

        // --- NOWE METODY: EDYCJA I USUWANIE ODPOWIEDZI ---

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditReply(Guid replyId, int guideId, string content)
        {
            var reply = await _context.Replies.FindAsync(replyId);
            if (reply == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (reply.UserId != user.Id) return Forbid();

            if (IsUserMuted(user))
            {
                TempData["Error"] = "Jesteś wyciszony.";
                return RedirectToAction("Details", new { id = guideId });
            }

            reply.Content = content;
            _context.Update(reply);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = guideId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReply(Guid replyId, int guideId)
        {
            var reply = await _context.Replies.FindAsync(replyId);
            if (reply == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            bool canDelete = reply.UserId == user.Id || User.IsInRole("Admin") || User.IsInRole("Moderator");

            if (!canDelete) return Forbid();

            _context.Replies.Remove(reply);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Odpowiedź została usunięta.";
            return RedirectToAction("Details", new { id = guideId });
        }
    }
}