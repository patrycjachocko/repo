using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using praca_dyplomowa.Data;
using praca_dyplomowa_zesp.Models.API;
using praca_dyplomowa_zesp.Models.Interactions.Reactions;
using praca_dyplomowa_zesp.Models.Modules.Games;
using praca_dyplomowa_zesp.Models.Users;
using praca_dyplomowa_zesp.Models.ViewModels;
using System;
using System.Collections.Generic; // Potrzebne dla List<>
using System.Linq;
using System.Threading.Tasks;

namespace praca_dyplomowa_zesp.Controllers
{
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IGDBClient _igdbClient; // Dodajemy klienta IGDB

        // Aktualizujemy konstruktor o IGDBClient
        public ReviewsController(ApplicationDbContext context, UserManager<User> userManager, IGDBClient igdbClient)
        {
            _context = context;
            _userManager = userManager;
            _igdbClient = igdbClient;
        }

        // GET: Lista recenzji
        public async Task<IActionResult> Index(long gameId, string gameName, string sortOrder = "best")
        {
            // 1. POBIERANIE RECENZJI
            var reviewsQuery = _context.GameReviews
                .Include(r => r.User)
                .Include(r => r.Reactions)
                .Where(r => r.IgdbGameId == gameId);

            var reviewsList = await reviewsQuery.ToListAsync();

            // Sortowanie recenzji
            switch (sortOrder)
            {
                case "newest":
                    reviewsList = reviewsList.OrderByDescending(r => r.CreatedAt).ToList();
                    break;
                case "popular":
                    reviewsList = reviewsList.OrderByDescending(r => r.Reactions.Count).ToList();
                    break;
                case "best":
                default:
                    reviewsList = reviewsList.OrderByDescending(r =>
                        (r.Reactions?.Count(x => x.Type == ReactionType.Like) ?? 0) -
                        (r.Reactions?.Count(x => x.Type == ReactionType.Dislike) ?? 0)
                    ).ToList();
                    break;
            }

            // 2. POBIERANIE DANYCH O OCENACH (NOWOŚĆ)
            var currentUserId = Guid.Empty;
            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                currentUserId = user?.Id ?? Guid.Empty;
            }

            // A. Dane z IGDB (Rating, AggregatedRating)
            double igdbUserRating = 0;
            double igdbCriticRating = 0;

            try
            {
                var gameQuery = $"fields rating, aggregated_rating; where id = {gameId}; limit 1;";
                var gameJsonResponse = await _igdbClient.ApiRequestAsync("games", gameQuery);
                if (!string.IsNullOrEmpty(gameJsonResponse))
                {
                    var games = JsonConvert.DeserializeObject<List<ApiGame>>(gameJsonResponse);
                    var gameInfo = games?.FirstOrDefault();
                    if (gameInfo != null)
                    {
                        igdbUserRating = gameInfo.Rating ?? 0;
                        igdbCriticRating = gameInfo.Aggregated_rating ?? 0;
                    }
                }
            }
            catch (Exception) { /* Ignorujemy błędy API, żeby nie wywaliło strony */ }

            // B. Oceny Lokalne
            var localRates = await _context.GameRates.Where(r => r.IgdbGameId == gameId).ToListAsync();
            double localAvg = localRates.Any() ? localRates.Average(r => r.Value) : 0;
            int localCount = localRates.Count;
            double personalRating = 0;

            if (currentUserId != Guid.Empty)
            {
                var myRate = localRates.FirstOrDefault(r => r.UserId == currentUserId);
                if (myRate != null) personalRating = myRate.Value;
            }

            // C. Budujemy ViewModel Ocen
            var ratingsModel = new GameRatingViewModel
            {
                IgdbGameId = gameId,
                IgdbUserRating = igdbUserRating,
                IgdbCriticRating = igdbCriticRating,
                LocalAverageRating = localAvg,
                LocalRatingCount = localCount,
                UserPersonalRating = personalRating
            };

            // PRZEKAZUJEMY DANE DO WIDOKU
            ViewBag.Ratings = ratingsModel; // <-- Przekazujemy oceny
            ViewBag.GameId = gameId;
            ViewBag.GameName = gameName;
            ViewBag.CurrentSort = sortOrder;
            ViewBag.CurrentUserId = currentUserId.ToString();

            return View(reviewsList);
        }

        // ... Reszta metod (Create, Edit, Delete, ToggleReaction) pozostaje BEZ ZMIAN ...
        // ... Skopiuj je z poprzedniej wersji, jeśli ich tu nie widzisz ...

        // POST: Dodaj recenzję
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(long gameId, string content, string gameName)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Recenzja nie może być pusta.";
                return RedirectToAction("Index", new { gameId = gameId, gameName = gameName });
            }

            var userId = _userManager.GetUserId(User);
            if (userId == null) return Challenge();

            var review = new GameReview
            {
                IgdbGameId = gameId,
                Content = content,
                UserId = Guid.Parse(userId),
                CreatedAt = DateTime.Now
            };

            _context.GameReviews.Add(review);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Dodano recenzję!";
            return RedirectToAction("Index", new { gameId = gameId, gameName = gameName });
        }

        // POST: Edytuj recenzję
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string content, string gameName)
        {
            var review = await _context.GameReviews.FindAsync(id);
            if (review == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            if (review.UserId.ToString() != userId) return Forbid();

            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Treść nie może być pusta.";
                return RedirectToAction("Index", new { gameId = review.IgdbGameId, gameName = gameName });
            }

            review.Content = content;
            _context.Update(review);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Zaktualizowano recenzję!";
            return RedirectToAction("Index", new { gameId = review.IgdbGameId, gameName = gameName });
        }

        // POST: Usuń recenzję
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string gameName)
        {
            var review = await _context.GameReviews.FindAsync(id);
            if (review == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var isAdminOrMod = User.IsInRole("Admin") || User.IsInRole("Moderator");

            if (review.UserId.ToString() != userId && !isAdminOrMod) return Forbid();

            _context.GameReviews.Remove(review);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Usunięto recenzję.";
            return RedirectToAction("Index", new { gameId = review.IgdbGameId, gameName = gameName });
        }

        // POST: Toggle Reaction
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleReaction(int reviewId, bool isUpvote, string returnUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var review = await _context.GameReviews.FindAsync(reviewId);
            if (review == null) return NotFound();

            var existingReaction = await _context.Reactions
                .FirstOrDefaultAsync(r => r.UserId == user.Id && r.GameReviewId == reviewId);

            if (existingReaction == null)
            {
                var newReaction = new Reaction
                {
                    UserId = user.Id,
                    GameReviewId = reviewId,
                    Type = isUpvote ? ReactionType.Like : ReactionType.Dislike
                };
                _context.Reactions.Add(newReaction);
            }
            else
            {
                var newType = isUpvote ? ReactionType.Like : ReactionType.Dislike;
                if (existingReaction.Type == newType) _context.Reactions.Remove(existingReaction);
                else
                {
                    existingReaction.Type = newType;
                    _context.Reactions.Update(existingReaction);
                }
            }
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Index", new { gameId = review.IgdbGameId, gameName = "Gra" });
        }
    }
}