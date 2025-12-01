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
            if (Guid.TryParse(userIdString, out Guid userId))
            {
                return userId;
            }
            return Guid.Empty;
        }

        // GET: Guides/Index/123
        public async Task<IActionResult> Index(long gameId)
        {
            if (gameId <= 0) return NotFound();

            // 1. Zapytanie do API: pobieramy też kategorię i kolekcje
            var gameQuery = $"fields name, parent_game, version_parent, category, collections.id; where id = {gameId}; limit 1;";

            var gameJsonResponse = await _igdbClient.ApiRequestAsync("games", gameQuery);
            var gameDetailsFromApi = (JsonConvert.DeserializeObject<List<ApiGame>>(gameJsonResponse) ?? new List<ApiGame>()).FirstOrDefault();

            if (gameDetailsFromApi == null) return NotFound();

            // 2. PRZEKIEROWANIE (DLC / Wersja / Bundle -> Gra Główna)

            // A: Standardowy rodzic (DLC)
            if (gameDetailsFromApi.Parent_game.HasValue)
            {
                return RedirectToAction("Index", new { gameId = gameDetailsFromApi.Parent_game.Value });
            }

            // B: Rodzic wersji (np. Legacy Edition)
            if (gameDetailsFromApi.Version_parent.HasValue)
            {
                return RedirectToAction("Index", new { gameId = gameDetailsFromApi.Version_parent.Value });
            }

            // C: FALLBACK DLA BUNDLI (np. The Sims 4 Halloween Bundle)
            // Jeśli to nie jest gra główna (Category != 0) i nie ma rodzica, szukamy w kolekcji
            if (gameDetailsFromApi.Category != 0 && gameDetailsFromApi.Collections != null && gameDetailsFromApi.Collections.Any())
            {
                // Bierzemy pierwszą kolekcję (zazwyczaj to seria, np. "The Sims")
                var collectionId = gameDetailsFromApi.Collections.First().Id;

                // Pobieramy TYLKO główne gry (Category = 0) z tej kolekcji
                var collectionQuery = $"fields id, name; where collections = ({collectionId}) & category = 0; limit 50;";
                var collectionJson = await _igdbClient.ApiRequestAsync("games", collectionQuery);

                if (!string.IsNullOrEmpty(collectionJson))
                {
                    var mainGamesInCollection = JsonConvert.DeserializeObject<List<ApiGame>>(collectionJson);
                    if (mainGamesInCollection != null)
                    {
                        // Szukamy gry głównej, której nazwa zawiera się w nazwie naszego pakietu
                        // Np. "The Sims 4 Halloween Bundle" zawiera "The Sims 4"
                        var matchedMainGame = mainGamesInCollection
                            .OrderByDescending(g => g.Name.Length) // Najpierw najdłuższe nazwy, żeby "The Sims 4" wygrało z "The Sims"
                            .FirstOrDefault(g => gameDetailsFromApi.Name.Contains(g.Name));

                        if (matchedMainGame != null && matchedMainGame.Id != gameId)
                        {
                            return RedirectToAction("Index", new { gameId = matchedMainGame.Id });
                        }
                    }
                }
            }

            // --- Kod dla gry głównej (lub gdy nie znaleziono rodzica) ---

            bool isInLibrary = false;
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId != Guid.Empty)
                {
                    isInLibrary = await _context.GamesInLibraries
                        .AnyAsync(g => g.UserId == currentUserId && g.IgdbGameId == gameId);
                }
            }

            var guidesFromDb = await _context.Guides
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
    }
}