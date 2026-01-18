using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using praca_dyplomowa_zesp.Models.API;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using praca_dyplomowa_zesp.Models.Modules.Guides;
using praca_dyplomowa_zesp.Models.Interactions.Rates;
using praca_dyplomowa_zesp.Models.Interactions.Comments;
using praca_dyplomowa_zesp.Models.Interactions.Reactions;
using Rotativa.AspNetCore;
using praca_dyplomowa_zesp.Data;
using praca_dyplomowa_zesp.Models.Modules.Users;
using praca_dyplomowa_zesp.Models.ViewModels.Guides;

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

        #region Private Helpers

        private Guid GetCurrentUserId()
        {
            var userIdString = _userManager.GetUserId(User);
            return Guid.TryParse(userIdString, out Guid userId) ? userId : Guid.Empty;
        }

        private bool IsUserMuted(User user)
        {
            if (user == null) return false;
            //weryfikacja czy data zakonczenia wyciszenia jest wciaz aktualna
            return user.BanEnd.HasValue && user.BanEnd.Value > DateTimeOffset.Now;
        }

        #endregion

        #region Main Views (List & Details)

        public async Task<IActionResult> Index(long gameId, string searchString, string sortOrder)
        {
            if (gameId <= 0) return NotFound();

            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentSort"] = sortOrder;

            var gameQuery = $"fields name, parent_game, version_parent, category, collections.id; where id = {gameId}; limit 1;";
            var gameJsonResponse = await _igdbClient.ApiRequestAsync("games", gameQuery);
            var gameDetails = (JsonConvert.DeserializeObject<List<IGDBGameDtos>>(gameJsonResponse) ?? new List<IGDBGameDtos>()).FirstOrDefault();

            if (gameDetails == null) return NotFound();

            //przekierowanie do gry matki jesli aktualne id to tylko dodatek lub inna wersja
            if (gameDetails.Parent_game.HasValue)
                return RedirectToAction(nameof(Index), new { gameId = gameDetails.Parent_game.Value });

            if (gameDetails.Version_parent.HasValue)
                return RedirectToAction(nameof(Index), new { gameId = gameDetails.Version_parent.Value });

            if (gameDetails.Category != 0 && gameDetails.Collections != null && gameDetails.Collections.Any())
            {
                //proba odnalezienia glownej gry w obrebie tej samej kolekcji
                var collectionId = gameDetails.Collections.First().Id;
                var collectionQuery = $"fields id, name; where collections = ({collectionId}) & category = 0; limit 50;";
                var collectionJson = await _igdbClient.ApiRequestAsync("games", collectionQuery);

                if (!string.IsNullOrEmpty(collectionJson))
                {
                    var mainGames = JsonConvert.DeserializeObject<List<IGDBGameDtos>>(collectionJson);
                    var matched = mainGames?.OrderByDescending(g => g.Name.Length).FirstOrDefault(g => gameDetails.Name.Contains(g.Name));
                    if (matched != null && matched.Id != gameId) return RedirectToAction(nameof(Index), new { gameId = matched.Id });
                }
            }

            var currentUserId = GetCurrentUserId();
            bool canSeeAllPending = User.IsInRole("Admin") || User.IsInRole("Moderator");
            bool isInLibrary = currentUserId != Guid.Empty && await _context.GamesInLibraries.AnyAsync(g => g.UserId == currentUserId && g.IgdbGameId == gameId);

            //selekcja poradnikow ktore sa zatwierdzone lub naleza do zalogowanego uzytkownika
            var guidesQuery = _context.Guides
                .Include(g => g.User)
                .Include(g => g.Rates)
                .Where(g => g.IgdbGameId == gameId)
                .Where(g => (g.IsApproved && !g.IsDeleted) || (g.UserId == currentUserId && !g.IsDeleted) || canSeeAllPending);

            if (!string.IsNullOrEmpty(searchString))
            {
                var term = searchString.ToLower();
                guidesQuery = guidesQuery.Where(s => s.Title.ToLower().Contains(term) || s.Content.ToLower().Contains(term));
            }

            var guidesList = await guidesQuery.ToListAsync();

            //reczne sortowanie pobranej listy w zaleznosci od wybranego parametru
            guidesList = sortOrder switch
            {
                "mine" => currentUserId != Guid.Empty
                    ? guidesList.OrderByDescending(g => g.UserId == currentUserId).ThenByDescending(g => g.CreatedAt).ToList()
                    : guidesList.OrderByDescending(g => g.CreatedAt).ToList(),
                "rating_desc" => guidesList.OrderByDescending(g => g.Rates.Any() ? g.Rates.Average(r => r.Value) : 0).ToList(),
                _ => guidesList.OrderByDescending(g => g.CreatedAt).ToList(),
            };

            var tips = await _context.Tips
                .Include(t => t.User)
                .Include(t => t.Reactions)
                .Where(t => t.IgdbGameId == gameId)
                .OrderByDescending(t => t.CreatedAt)
                .Take(20)
                .ToListAsync();

            return View(new GuidesViewModel
            {
                IgdbGameId = gameId,
                GameName = gameDetails.Name ?? "Brak nazwy",
                IsInLibrary = isInLibrary,
                Guides = guidesList,
                Tips = tips
            });
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var guide = await _context.Guides
                .Include(g => g.User)
                .Include(g => g.Rates)
                .FirstOrDefaultAsync(m => m.Id == id);

            //blokada dostepu do usunietych poradnikow dla zwyklych uzytkownikow
            if (guide == null || (guide.IsDeleted && !User.IsInRole("Admin") && !User.IsInRole("Moderator")))
                return NotFound();

            var comments = await _context.Comments
                .Where(c => c.GuideId == id)
                .Include(c => c.Author)
                .Include(c => c.Reactions)
                .Include(c => c.Replies).ThenInclude(r => r.Author)
                .Include(c => c.Replies).ThenInclude(r => r.Reactions)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            double userRating = 0;
            if (User.Identity.IsAuthenticated)
            {
                var userId = GetCurrentUserId();
                userRating = (await _context.Rates.FirstOrDefaultAsync(r => r.GuideId == id && r.UserId == userId))?.Value ?? 0;
            }

            return View(new GuideDetailsViewModel
            {
                Guide = guide,
                Comments = comments,
                UserRating = userRating,
                AverageRating = guide.Rates.Any() ? guide.Rates.Average(r => r.Value) : 0
            });
        }

        #endregion

        #region Guide CRUD & Lifecycle

        [Authorize]
        public async Task<IActionResult> Create(long gameId)
        {
            if (gameId <= 0) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (IsUserMuted(user))
            {
                TempData["Error"] = $"Twoje konto jest wyciszone do {user.BanEnd?.ToString("dd.MM.yyyy HH:mm")}.";
                return RedirectToAction(nameof(Index), new { gameId = gameId });
            }

            return View(new Guide { IgdbGameId = gameId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guide guide, IFormFile? coverImageFile, string action)
        {
            var user = await _userManager.GetUserAsync(User);
            if (IsUserMuted(user))
            {
                ModelState.AddModelError(string.Empty, "Twoje konto jest wyciszone.");
                return View(guide);
            }

            ModelState.Remove("User");
            ModelState.Remove("CoverImage");

            if (coverImageFile == null || coverImageFile.Length == 0)
                ModelState.AddModelError("CoverImage", "Okładka poradnika jest wymagana!");

            if (!ModelState.IsValid) return View(guide);

            guide.UserId = user.Id;
            guide.CreatedAt = DateTime.Now;

            //nadanie statusu szkicu lub wyslanie do zatwierdzenia przez administracje
            if (action == "draft")
            {
                guide.IsDraft = true;
                guide.IsApproved = false;
                TempData["StatusMessage"] = "Zapisano jako szkic.";
            }
            else
            {
                guide.IsDraft = false;
                //automatyczne zatwierdzenie tresci jesli autorem jest admin lub moderator
                guide.IsApproved = User.IsInRole("Admin") || User.IsInRole("Moderator");
                TempData["StatusMessage"] = guide.IsApproved ? "Opublikowano poradnik!" : "Przesłano do akceptacji.";
            }

            using (var memoryStream = new MemoryStream())
            {
                //konwersja pliku graficznego na bajty do zapisu w bazie danych
                await coverImageFile.CopyToAsync(memoryStream);
                guide.CoverImage = memoryStream.ToArray();
                guide.CoverImageContentType = coverImageFile.ContentType;
            }

            _context.Add(guide);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { gameId = guide.IgdbGameId });
        }

        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var guide = await _context.Guides.FindAsync(id);
            if (guide == null) return NotFound();

            if (guide.UserId != GetCurrentUserId() && !User.IsInRole("Admin") && !User.IsInRole("Moderator"))
                return Forbid();

            return View(guide);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Guide guide, IFormFile? coverImageFile, string action)
        {
            if (id != guide.Id) return NotFound();

            ModelState.Remove("User");
            ModelState.Remove("CoverImage");

            if (!ModelState.IsValid) return View(guide);

            var existingGuide = await _context.Guides.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id);
            if (existingGuide == null) return NotFound();

            bool isAdminOrMod = User.IsInRole("Admin") || User.IsInRole("Moderator");
            if (existingGuide.UserId != GetCurrentUserId() && !isAdminOrMod) return Forbid();

            guide.UserId = existingGuide.UserId;
            guide.CreatedAt = existingGuide.CreatedAt;
            guide.UpdatedAt = DateTime.Now;
            guide.IsDeleted = existingGuide.IsDeleted;
            guide.DeletedAt = existingGuide.DeletedAt;

            if (action == "draft")
            {
                guide.IsDraft = true;
                guide.IsApproved = false;
                guide.IsRejected = existingGuide.IsRejected;
                guide.RejectionReason = existingGuide.RejectionReason;
                TempData["StatusMessage"] = "Zaktualizowano szkic.";
            }
            else
            {
                guide.IsDraft = false;
                guide.IsRejected = false;
                guide.RejectionReason = null;

                if (existingGuide.IsDraft || existingGuide.IsRejected)
                {
                    //ponowne wyslanie do akceptacji po poprawkach autora
                    guide.IsApproved = isAdminOrMod;
                    TempData["StatusMessage"] = guide.IsApproved ? "Opublikowano!" : "Przesłano do ponownej akceptacji.";
                }
                else
                {
                    guide.IsApproved = existingGuide.IsApproved;
                    TempData["StatusMessage"] = "Zapisano zmiany.";
                }
            }

            if (coverImageFile != null && coverImageFile.Length > 0)
            {
                using var memoryStream = new MemoryStream();
                await coverImageFile.CopyToAsync(memoryStream);
                guide.CoverImage = memoryStream.ToArray();
                guide.CoverImageContentType = coverImageFile.ContentType;
            }
            else
            {
                //zachowanie starej okladki jesli nowa nie zostala przeslana
                guide.CoverImage = existingGuide.CoverImage;
                guide.CoverImageContentType = existingGuide.CoverImageContentType;
            }

            _context.Update(guide);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { gameId = guide.IgdbGameId });
        }

        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var guide = await _context.Guides.Include(g => g.User).FirstOrDefaultAsync(m => m.Id == id);
            if (guide == null) return NotFound();

            if (guide.UserId != GetCurrentUserId() && !User.IsInRole("Admin") && !User.IsInRole("Moderator"))
                return Forbid();

            return View(guide);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide == null) return RedirectToAction(nameof(Index), "Games");

            if (guide.UserId != GetCurrentUserId() && !User.IsInRole("Admin") && !User.IsInRole("Moderator"))
                return Forbid();

            long gameId = guide.IgdbGameId;

            if (guide.IsApproved)
            {
                //miekkie usuwanie dla tresci publicznych (przeniesienie do kosza)
                guide.IsDeleted = true;
                guide.DeletedAt = DateTime.Now;
                _context.Update(guide);
                TempData["StatusMessage"] = "Poradnik przeniesiony do kosza.";
            }
            else
            {
                //trwale usuwanie szkicow i tresci niezaakceptowanych
                _context.Guides.Remove(guide);
                TempData["StatusMessage"] = "Poradnik został trwale usunięty.";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { gameId = gameId });
        }

        #endregion

        #region Tip Management

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTip(long gameId, string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return RedirectToAction(nameof(Index), new { gameId });

            var user = await _userManager.GetUserAsync(User);
            if (IsUserMuted(user))
            {
                TempData["Error"] = $"Jesteś wyciszony do {user.BanEnd?.ToString("dd.MM.yyyy HH:mm")}.";
                return RedirectToAction(nameof(Index), new { gameId });
            }

            _context.Tips.Add(new Tip { IgdbGameId = gameId, UserId = user.Id, Content = content, CreatedAt = DateTime.Now });
            await _context.SaveChangesAsync();
            TempData["Success"] = "Dodano wskazówkę!";
            return RedirectToAction(nameof(Index), new { gameId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTip(int id, string content)
        {
            var tip = await _context.Tips.FindAsync(id);
            var user = await _userManager.GetUserAsync(User);
            if (tip == null || user == null) return NotFound();

            if (tip.UserId != user.Id && !User.IsInRole("Admin") && !User.IsInRole("Moderator")) return Forbid();
            if (IsUserMuted(user)) return Forbid();

            if (!string.IsNullOrWhiteSpace(content))
            {
                tip.Content = content;
                _context.Update(tip);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Zaktualizowano wskazówkę.";
            }

            return RedirectToAction(nameof(Index), new { gameId = tip.IgdbGameId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTip(int id)
        {
            var tip = await _context.Tips.Include(t => t.Reactions).FirstOrDefaultAsync(t => t.Id == id);
            if (tip == null) return NotFound();

            if (tip.UserId != GetCurrentUserId() && !User.IsInRole("Admin") && !User.IsInRole("Moderator")) return Forbid();

            long gameId = tip.IgdbGameId;
            //usuniecie wszystkich powiazanych reakcji przed usunieciem wskazowki
            if (tip.Reactions != null && tip.Reactions.Any()) _context.Reactions.RemoveRange(tip.Reactions);

            _context.Tips.Remove(tip);
            await _context.SaveChangesAsync();
            TempData["StatusMessage"] = "Wskazówka została usunięta.";
            return RedirectToAction(nameof(Index), new { gameId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleTipReaction(int tipId, bool isUpvote)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || IsUserMuted(user)) return Forbid();

            var tip = await _context.Tips.FindAsync(tipId);
            if (tip == null) return NotFound();

            var reaction = await _context.Reactions.FirstOrDefaultAsync(r => r.TipId == tipId && r.UserId == user.Id);
            var targetType = isUpvote ? ReactionType.Like : ReactionType.Dislike;

            if (reaction != null)
            {
                //usuniecie reakcji jesli uzytkownik kliknal w ten sam typ ponownie
                if (reaction.Type == targetType) _context.Reactions.Remove(reaction);
                else { reaction.Type = targetType; _context.Update(reaction); }
            }
            else
            {
                _context.Reactions.Add(new Reaction { UserId = user.Id, TipId = tipId, Type = targetType });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { gameId = tip.IgdbGameId });
        }

        #endregion

        #region Interactions (Ratings, Comments & Reactions)

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RateGuide([FromBody] RateRequest request)
        {
            if (request == null) return BadRequest();

            //normalizacja oceny do skali 1-5 z krokiem co 0.5
            double val = Math.Clamp(request.RatingValue, 1, 5);
            val = Math.Round(val * 2, MidpointRounding.AwayFromZero) / 2;

            var user = await _userManager.GetUserAsync(User);
            var guide = await _context.Guides.Include(g => g.Rates).FirstOrDefaultAsync(g => g.Id == request.GuideId);
            if (user == null || guide == null) return NotFound();

            var existingRate = await _context.Rates.FirstOrDefaultAsync(r => r.GuideId == request.GuideId && r.UserId == user.Id);
            if (existingRate != null) { existingRate.Value = val; _context.Rates.Update(existingRate); }
            else _context.Rates.Add(new Rate { UserId = user.Id, GuideId = request.GuideId, Value = val, CreatedAt = DateTime.Now });

            await _context.SaveChangesAsync();
            return Ok(new { success = true, newAverage = guide.Rates.Any() ? guide.Rates.Average(r => r.Value) : val });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int guideId, string content)
        {
            var user = await _userManager.GetUserAsync(User);
            if (IsUserMuted(user))
            {
                TempData["Error"] = "Twoje konto jest wyciszone. Nie możesz dodawać komentarzy.";
                return RedirectToAction(nameof(Details), new { id = guideId });
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                _context.Comments.Add(new Comment { GuideId = guideId, UserId = user.Id, Content = content, CreatedAt = DateTime.Now });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Details), new { id = guideId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddReply(Guid commentId, int guideId, string content)
        {
            var user = await _userManager.GetUserAsync(User);
            if (IsUserMuted(user))
            {
                TempData["Error"] = "Twoje konto jest wyciszone.";
                return RedirectToAction(nameof(Details), new { id = guideId });
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                _context.Add(new Reply { ParentCommentId = commentId, UserId = user.Id, Content = content, CreatedAt = DateTime.Now });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Details), new { id = guideId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleReaction(Guid? commentId, Guid? replyId, int guideId, bool isUpvote)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || IsUserMuted(user)) return Forbid();

            //wspolna logika reakcji dla komentarzy glownych oraz odpowiedzi
            Reaction reaction = commentId.HasValue
                ? await _context.Reactions.FirstOrDefaultAsync(r => r.CommentId == commentId.Value && r.UserId == user.Id)
                : await _context.Reactions.FirstOrDefaultAsync(r => r.ReplyId == replyId.Value && r.UserId == user.Id);

            var targetType = isUpvote ? ReactionType.Like : ReactionType.Dislike;

            if (reaction != null)
            {
                if (reaction.Type == targetType) _context.Reactions.Remove(reaction);
                else { reaction.Type = targetType; _context.Reactions.Update(reaction); }
            }
            else
            {
                _context.Reactions.Add(new Reaction { UserId = user.Id, Type = targetType, CommentId = commentId, ReplyId = replyId });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = guideId, section = "comments" });
        }

        #endregion

        #region PDF Generation & Administrative Actions

        public async Task<IActionResult> DownloadPdf(int id)
        {
            var guide = await _context.Guides.Include(g => g.User).Include(g => g.Rates).FirstOrDefaultAsync(m => m.Id == id);
            if (guide == null) return NotFound();

            var viewModel = new GuideDetailsViewModel { Guide = guide, AverageRating = guide.Rates.Any() ? guide.Rates.Average(r => r.Value) : 0 };
            //generowanie pliku pdf na podstawie dedykowanego widoku html
            return new ViewAsPdf("DetailsPdf", viewModel)
            {
                FileName = $"Poradnik_{guide.Title}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageMargins = new Rotativa.AspNetCore.Options.Margins(10, 10, 10, 10)
            };
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> ApproveGuide(int id)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide == null) return NotFound();

            guide.IsApproved = true;
            guide.IsRejected = false;
            guide.RejectionReason = null;

            _context.Update(guide);
            await _context.SaveChangesAsync();
            //powrot do strony z ktorej przyszlo zadanie zatwierdzenia
            return Redirect(Request.Headers["Referer"].ToString());
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectGuide(int id, string reason)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide == null) return NotFound();

            guide.IsApproved = false;
            guide.IsRejected = true;
            guide.RejectionReason = reason;

            _context.Update(guide);
            await _context.SaveChangesAsync();
            TempData["StatusMessage"] = "Poradnik został odrzucony i zwrócony do autora.";
            return RedirectToAction(nameof(Details), new { id = id });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> DeletePermanently(int id)
        {
            var guide = await _context.Guides.FindAsync(id);
            if (guide == null) return RedirectToAction(nameof(Index), "Games");

            long gameId = guide.IgdbGameId;
            _context.Guides.Remove(guide);
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Poradnik został usunięty permanentnie.";
            return RedirectToAction(nameof(Index), new { gameId = gameId });
        }

        #endregion

        #region Comment/Reply CRUD

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditComment(Guid commentId, int guideId, string content)
        {
            var comment = await _context.Comments.FindAsync(commentId);
            var user = await _userManager.GetUserAsync(User);
            if (comment == null || user == null || comment.UserId != user.Id) return Forbid();
            if (IsUserMuted(user)) return Forbid();

            comment.Content = content;
            _context.Update(comment);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = guideId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(Guid commentId, int guideId)
        {
            var comment = await _context.Comments.FindAsync(commentId);
            if (comment == null) return NotFound();
            if (comment.UserId != GetCurrentUserId() && !User.IsInRole("Admin") && !User.IsInRole("Moderator")) return Forbid();

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Komentarz został usunięty.";
            return RedirectToAction(nameof(Details), new { id = guideId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditReply(Guid replyId, int guideId, string content)
        {
            var reply = await _context.Replies.FindAsync(replyId);
            var user = await _userManager.GetUserAsync(User);
            if (reply == null || user == null || reply.UserId != user.Id) return Forbid();
            if (IsUserMuted(user)) return Forbid();

            reply.Content = content;
            _context.Update(reply);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = guideId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReply(Guid replyId, int guideId)
        {
            var reply = await _context.Replies.FindAsync(replyId);
            if (reply == null) return NotFound();
            if (reply.UserId != GetCurrentUserId() && !User.IsInRole("Admin") && !User.IsInRole("Moderator")) return Forbid();

            _context.Replies.Remove(reply);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Odpowiedź została usunięta.";
            return RedirectToAction(nameof(Details), new { id = guideId });
        }

        #endregion

        public class RateRequest
        {
            public int GuideId { get; set; }
            public double RatingValue { get; set; }
        }
    }
}