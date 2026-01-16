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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace praca_dyplomowa_zesp.Controllers
{
    /// <summary>
    /// Kontroler obsługujący recenzje gier, ich sortowanie oraz reakcje użytkowników (lajki/dislajki).
    /// </summary>
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IGDBClient _igdbClient;

        public ReviewsController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IGDBClient igdbClient)
        {
            _context = context;
            _userManager = userManager;
            _igdbClient = igdbClient;
        }

        #region Core Actions

        /// <summary>
        /// Wyświetla listę recenzji dla danej gry wraz ze statystykami ocen.
        /// </summary>
        public async Task<IActionResult> Index(long gameId, string gameName, string sortOrder = "best")
        {
            // Pobieranie i dołączanie powiązanych danych recenzji
            var reviewsQuery = _context.GameReviews
                .Include(r => r.User)
                .Include(r => r.Reactions)
                .Where(r => r.IgdbGameId == gameId);

            var reviewsList = await reviewsQuery.ToListAsync();
            SortReviews(ref reviewsList, sortOrder);

            var user = await _userManager.GetUserAsync(User);
            var currentUserId = user?.Id ?? Guid.Empty;

            // Przygotowanie danych o ocenach (IGDB + Lokalne)
            var ratingsModel = await GetGameRatingsViewModel(gameId, currentUserId);

            ViewBag.Ratings = ratingsModel;
            ViewBag.GameId = gameId;
            ViewBag.GameName = gameName;
            ViewBag.CurrentSort = sortOrder;
            ViewBag.CurrentUserId = currentUserId.ToString();

            return View(reviewsList);
        }

        #endregion

        #region Review CRUD

        /// <summary>
        /// Dodaje nową recenzję do bazy danych.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(long gameId, string content, string gameName)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Recenzja nie może być pusta.";
                return RedirectToIndex(gameId, gameName);
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
            return RedirectToIndex(gameId, gameName);
        }

        /// <summary>
        /// Aktualizuje treść istniejącej recenzji (tylko dla właściciela).
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string content, string gameName)
        {
            var review = await _context.GameReviews.FindAsync(id);
            if (review == null) return NotFound();

            if (review.UserId.ToString() != _userManager.GetUserId(User)) return Forbid();

            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Treść nie może być pusta.";
                return RedirectToIndex(review.IgdbGameId, gameName);
            }

            review.Content = content;
            _context.Update(review);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Zaktualizowano recenzję!";
            return RedirectToIndex(review.IgdbGameId, gameName);
        }

        /// <summary>
        /// Usuwa recenzję z bazy (dla właściciela lub administracji).
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string gameName)
        {
            var review = await _context.GameReviews.FindAsync(id);
            if (review == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            bool isAdminOrMod = User.IsInRole("Admin") || User.IsInRole("Moderator");

            if (review.UserId.ToString() != userId && !isAdminOrMod) return Forbid();

            _context.GameReviews.Remove(review);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Usunięto recenzję.";
            return RedirectToIndex(review.IgdbGameId, gameName);
        }

        #endregion

        #region Interactions

        /// <summary>
        /// Obsługuje dodawanie/usuwanie reakcji (Like/Dislike) pod recenzją.
        /// </summary>
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

            var targetType = isUpvote ? ReactionType.Like : ReactionType.Dislike;

            if (existingReaction == null)
            {
                _context.Reactions.Add(new Reaction
                {
                    UserId = user.Id,
                    GameReviewId = reviewId,
                    Type = targetType
                });
            }
            else
            {
                if (existingReaction.Type == targetType)
                {
                    _context.Reactions.Remove(existingReaction);
                }
                else
                {
                    existingReaction.Type = targetType;
                    _context.Update(existingReaction);
                }
            }

            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToIndex(review.IgdbGameId, "Gra");
        }

        #endregion

        #region Private Helpers

        private void SortReviews(ref List<GameReview> reviews, string sortOrder)
        {
            reviews = sortOrder switch
            {
                "newest" => reviews.OrderByDescending(r => r.CreatedAt).ToList(),
                "popular" => reviews.OrderByDescending(r => r.Reactions.Count).ToList(),
                _ => reviews.OrderByDescending(r =>
                        (r.Reactions?.Count(x => x.Type == ReactionType.Like) ?? 0) -
                        (r.Reactions?.Count(x => x.Type == ReactionType.Dislike) ?? 0))
                    .ThenByDescending(r => r.CreatedAt).ToList()
            };
        }

        private async Task<GameRatingViewModel> GetGameRatingsViewModel(long gameId, Guid currentUserId)
        {
            var model = new GameRatingViewModel { IgdbGameId = gameId };

            // Dane z IGDB
            try
            {
                var query = $"fields rating, aggregated_rating; where id = {gameId}; limit 1;";
                var json = await _igdbClient.ApiRequestAsync("games", query);
                if (!string.IsNullOrEmpty(json))
                {
                    var gameInfo = JsonConvert.DeserializeObject<List<ApiGame>>(json)?.FirstOrDefault();
                    if (gameInfo != null)
                    {
                        model.IgdbUserRating = gameInfo.Rating ?? 0;
                        model.IgdbCriticRating = gameInfo.Aggregated_rating ?? 0;
                    }
                }
            }
            catch { /* API Errors silent fail */ }

            // Dane lokalne
            var localRates = await _context.GameRates.Where(r => r.IgdbGameId == gameId).ToListAsync();
            model.LocalAverageRating = localRates.Any() ? localRates.Average(r => r.Value) : 0;
            model.LocalRatingCount = localRates.Count;
            model.UserPersonalRating = localRates.FirstOrDefault(r => r.UserId == currentUserId)?.Value ?? 0;

            return model;
        }

        private IActionResult RedirectToIndex(long gameId, string gameName)
            => RedirectToAction(nameof(Index), new { gameId, gameName });

        #endregion
    }
}