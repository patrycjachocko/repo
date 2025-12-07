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
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var uid = GetCurrentUserId();
                if (uid != Guid.Empty) isInLibrary = await _context.GamesInLibraries.AnyAsync(g => g.UserId == uid && g.IgdbGameId == gameId);
            }

            // --- BUDOWANIE ZAPYTANIA (tylko filtrowanie) ---
            var guidesQuery = _context.Guides
                .Include(g => g.User)
                .Include(g => g.Rates) // Ważne: pobieramy oceny, żeby móc po nich posortować w pamięci
                .Where(g => g.IgdbGameId == gameId)
                .AsQueryable();

            // 1. Wyszukiwanie (to może zostać w SQL, działa dobrze)
            if (!string.IsNullOrEmpty(searchString))
            {
                // Zamieniamy frazę wyszukiwania na małe litery
                var term = searchString.ToLower();

                // Sprawdzamy, czy tytuł (zmieniony na małe) LUB treść (zmieniona na małe) zawiera frazę
                guidesQuery = guidesQuery.Where(s =>
                    s.Title.ToLower().Contains(term) ||
                    s.Content.ToLower().Contains(term));
            }

            // 2. POBRANIE DANYCH DO PAMIĘCI
            // Wywołujemy ToListAsync TERAZ, przed sortowaniem, aby uniknąć błędu translacji LINQ
            var guidesFromDb = await guidesQuery.ToListAsync();

            // 3. SORTOWANIE W PAMIĘCI (LINQ to Objects)
            // Tutaj C# radzi sobie świetnie z matematyką i nullami
            switch (sortOrder)
            {
                case "rating_desc":
                    // Sortujemy malejąco po średniej. Jeśli brak ocen (Any jest false), przyjmujemy 0.
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
                .Include(c => c.Replies).ThenInclude(r => r.Author)
                .Include(c => c.Reactions)
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

            var reply = new Reply
            {
                ParentCommentId = commentId,
                UserId = user.Id,
                Content = content,
                CreatedAt = DateTime.Now
            };

            // Zakładam, że masz DbSet<Reply> w Context, jeśli nie - dodaj go,
            // lub użyj _context.Set<Reply>().Add(reply)
            _context.Add(reply);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = guideId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleReaction(Guid commentId, int guideId, bool isUpvote)
        {
            var user = await _userManager.GetUserAsync(User);

            var existingReaction = await _context.Reactions
                .FirstOrDefaultAsync(r => r.CommentId == commentId && r.UserId == user.Id);

            var targetType = isUpvote ? ReactionType.Like : ReactionType.Dislike;

            if (existingReaction != null)
            {
                if (existingReaction.Type == targetType)
                {
                    _context.Reactions.Remove(existingReaction); // Usuń, jeśli kliknięto to samo (odznacz)
                }
                else
                {
                    existingReaction.Type = targetType; // Zmień decyzję
                    _context.Reactions.Update(existingReaction);
                }
            }
            else
            {
                var reaction = new Reaction
                {
                    CommentId = commentId,
                    UserId = user.Id,
                    Type = targetType
                };
                _context.Reactions.Add(reaction);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Details", new { id = guideId });
        }

        // --- POZOSTAŁE METODY (Create, Edit, Delete) - BEZ ZMIAN ---

        // GET: Create
        [Authorize]
        public IActionResult Create(long gameId)
        {
            if (gameId <= 0) return NotFound();
            return View(new Guide { IgdbGameId = gameId });
        }

        // POST: Create
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guide guide, IFormFile? coverImageFile)
        {
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
            if (guide.UserId != currentUserId && !User.IsInRole("Admin"))
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

            ModelState.Remove("User");
            ModelState.Remove("CoverImage");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingGuide = await _context.Guides.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
                    if (existingGuide == null) return NotFound();

                    var currentUserId = GetCurrentUserId();
                    if (existingGuide.UserId != currentUserId && !User.IsInRole("Admin")) return Forbid();

                    guide.UserId = existingGuide.UserId;
                    guide.CreatedAt = existingGuide.CreatedAt;
                    guide.UpdatedAt = DateTime.Now;

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

                    _context.Update(guide);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Guides.Any(e => e.Id == guide.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Details), new { id = guide.Id });
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
            if (guide.UserId != currentUserId && !User.IsInRole("Admin")) return Forbid();

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
                if (guide.UserId != currentUserId && !User.IsInRole("Admin")) return Forbid();

                long gameId = guide.IgdbGameId;
                _context.Guides.Remove(guide);
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
    }
}