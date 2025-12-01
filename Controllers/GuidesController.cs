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
        public async Task<IActionResult> Index(long gameId)
        {
            if (gameId <= 0) return NotFound();

            // Pobieranie danych o grze (z logiką fallback dla Bundli i DLC)
            var gameQuery = $"fields name, parent_game, version_parent, category, collections.id; where id = {gameId}; limit 1;";
            var gameJsonResponse = await _igdbClient.ApiRequestAsync("games", gameQuery);
            var gameDetailsFromApi = (JsonConvert.DeserializeObject<List<ApiGame>>(gameJsonResponse) ?? new List<ApiGame>()).FirstOrDefault();

            if (gameDetailsFromApi == null) return NotFound();

            // Logika przekierowań (DLC -> Main)
            if (gameDetailsFromApi.Parent_game.HasValue)
                return RedirectToAction("Index", new { gameId = gameDetailsFromApi.Parent_game.Value });
            if (gameDetailsFromApi.Version_parent.HasValue)
                return RedirectToAction("Index", new { gameId = gameDetailsFromApi.Version_parent.Value });

            // Fallback dla Bundli
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

            bool isInLibrary = false;
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var uid = GetCurrentUserId();
                if (uid != Guid.Empty) isInLibrary = await _context.GamesInLibraries.AnyAsync(g => g.UserId == uid && g.IgdbGameId == gameId);
            }

            var guidesFromDb = await _context.Guides
                .Include(g => g.User) // Do wyświetlenia autora
                .Where(g => g.IgdbGameId == gameId)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();

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
                .FirstOrDefaultAsync(m => m.Id == id);

            if (guide == null) return NotFound();

            return View(guide);
        }

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
            ModelState.Remove("User"); // User jest ustawiany ręcznie
            ModelState.Remove("CoverImage"); // Obraz jest opcjonalny w formularzu (ale przetwarzany ręcznie)

            if (ModelState.IsValid)
            {
                guide.UserId = GetCurrentUserId();
                guide.CreatedAt = DateTime.Now;

                // Przetwarzanie zdjęcia okładkowego
                if (coverImageFile != null && coverImageFile.Length > 0)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await coverImageFile.CopyToAsync(memoryStream);
                        guide.CoverImage = memoryStream.ToArray();
                        guide.CoverImageContentType = coverImageFile.ContentType;
                    }
                }

                _context.Add(guide);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { gameId = guide.IgdbGameId });
            }
            return View(guide);
        }

        // GET: Edit/5
        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var guide = await _context.Guides.FindAsync(id);
            if (guide == null) return NotFound();

            // Zabezpieczenie: tylko autor lub admin może edytować
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

                    // Sprawdzenie uprawnień
                    var currentUserId = GetCurrentUserId();
                    if (existingGuide.UserId != currentUserId && !User.IsInRole("Admin")) return Forbid();

                    // Zachowaj dane, których nie ma w formularzu edycji
                    guide.UserId = existingGuide.UserId;
                    guide.CreatedAt = existingGuide.CreatedAt;
                    guide.UpdatedAt = DateTime.Now;

                    // Obsługa zdjęcia: jeśli nowe przesłano -> podmień, jeśli nie -> zachowaj stare
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

                long gameId = guide.IgdbGameId; // Zapamiętaj ID gry do powrotu
                _context.Guides.Remove(guide);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { gameId = gameId });
            }
            return RedirectToAction(nameof(Index), "Games");
        }
    }
}