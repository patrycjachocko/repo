using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using praca_dyplomowa_zesp.Data;
using praca_dyplomowa_zesp.Models.API;
using praca_dyplomowa_zesp.Models.Interactions.Reactions;
using praca_dyplomowa_zesp.Models.Modules.Games;
using praca_dyplomowa_zesp.Models.Modules.Users;
using praca_dyplomowa_zesp.Models.ViewModels.Games;

namespace praca_dyplomowa_zesp.Controllers
{
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
            //przypisanie wstrzyknietych serwisow do prywatnych pol klasy
            _context = context;
            _userManager = userManager;
            _igdbClient = igdbClient;
        }

        #region Core Actions

        public async Task<IActionResult> Index(long gameId, string gameName, string sortOrder = "best")
        {
            //pobranie recenzji z bazy wraz z powiazanymi danymi autorow i reakcji
            var reviewsQuery = _context.GameReviews
                .Include(r => r.User)
                .Include(r => r.Reactions)
                .Where(r => r.IgdbGameId == gameId);

            var reviewsList = await reviewsQuery.ToListAsync();
            //wywolanie metody sortujacej liste na podstawie parametru wejsciowego
            SortReviews(ref reviewsList, sortOrder);

            var user = await _userManager.GetUserAsync(User);
            var currentUserId = user?.Id ?? Guid.Empty;

            //pobranie modelu ocen zawierajacego dane z api oraz bazy lokalnej
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

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string content, string gameName)
        {
            var review = await _context.GameReviews.FindAsync(id);
            if (review == null) return NotFound();

            //blokada edycji dla uzytkownikow niebedacych autorami wpisu
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

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, string gameName)
        {
            var review = await _context.GameReviews.FindAsync(id);
            if (review == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            bool isAdminOrMod = User.IsInRole("Admin") || User.IsInRole("Moderator");

            //weryfikacja uprawnien do usuniecia wpisu przez autora lub administracje
            if (review.UserId.ToString() != userId && !isAdminOrMod) return Forbid();

            _context.GameReviews.Remove(review);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Usunięto recenzję.";
            return RedirectToIndex(review.IgdbGameId, gameName);
        }

        #endregion

        #region Interactions

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleReaction(int reviewId, bool isUpvote, string returnUrl)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var review = await _context.GameReviews.FindAsync(reviewId);
            if (review == null) return NotFound();

            //sprawdzenie czy uzytkownik juz wczesniej zareagowal na ten wpis
            var existingReaction = await _context.Reactions
                .FirstOrDefaultAsync(r => r.UserId == user.Id && r.GameReviewId == reviewId);

            var targetType = isUpvote ? ReactionType.Like : ReactionType.Dislike;

            if (existingReaction == null)
            {
                //dodanie nowej reakcji jesli wczesniej nie istniala
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
                    //usuniecie reakcji przy ponownym kliknieciu w ten sam przycisk
                    _context.Reactions.Remove(existingReaction);
                }
                else
                {
                    //zmiana typu reakcji na przeciwny
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
            //logika sortowania recenzji w pamieci na podstawie wybranych kryteriow
            reviews = sortOrder switch
            {
                "newest" => reviews.OrderByDescending(r => r.CreatedAt).ToList(),
                "popular" => reviews.OrderByDescending(r => r.Reactions.Count).ToList(),
                //domyslne sortowanie wedlug bilansu punktowego reakcji
                _ => reviews.OrderByDescending(r =>
                        (r.Reactions?.Count(x => x.Type == ReactionType.Like) ?? 0) -
                        (r.Reactions?.Count(x => x.Type == ReactionType.Dislike) ?? 0))
                    .ThenByDescending(r => r.CreatedAt).ToList()
            };
        }

        private async Task<GameRatingViewModel> GetGameRatingsViewModel(long gameId, Guid currentUserId)
        {
            var model = new GameRatingViewModel { IgdbGameId = gameId };

            try
            {
                //pobranie oficjalnych ocen z zewnetrznego serwisu igdb
                var query = $"fields rating, aggregated_rating; where id = {gameId}; limit 1;";
                var json = await _igdbClient.ApiRequestAsync("games", query);
                if (!string.IsNullOrEmpty(json))
                {
                    var gameInfo = JsonConvert.DeserializeObject<List<IGDBGameDtos>>(json)?.FirstOrDefault();
                    if (gameInfo != null)
                    {
                        model.IgdbUserRating = gameInfo.Rating ?? 0;
                        model.IgdbCriticRating = gameInfo.Aggregated_rating ?? 0;
                    }
                }
            }
            catch { }

            //pobranie i wyliczenie sredniej z ocen wystawionych lokalnie przez uzytkownikow
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