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

// Definiujemy ViewModel dla widoku Poradników
namespace praca_dyplomowa_zesp.Models.Modules.Guides
{
    public class GuidesViewModel
    {
        public long IgdbGameId { get; set; }
        public string GameName { get; set; } = "Nieznana gra";
        public bool IsInLibrary { get; set; } = false;
        public List<Guide> Guides { get; set; } = new List<Guide>();
    }
}


namespace praca_dyplomowa_zesp.Controllers
{
    [AllowAnonymous] // Każdy może przeglądać poradniki
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

        /// <summary>
        /// Metoda pomocnicza do pobierania ID (Guid) aktualnie zalogowanego użytkownika
        /// </summary>
        private Guid GetCurrentUserId()
        {
            var userIdString = _userManager.GetUserId(User);
            if (Guid.TryParse(userIdString, out Guid userId))
            {
                return userId;
            }
            return Guid.Empty;
        }

        // GET: Guides/Index/123 (gdzie 123 to IgdbGameId)
        public async Task<IActionResult> Index(long gameId)
        {
            if (gameId <= 0) return NotFound();

            // 1. Pobierz nazwę gry z API
            var gameQuery = $"fields name; where id = {gameId}; limit 1;";
            var gameJsonResponse = await _igdbClient.ApiRequestAsync("games", gameQuery);
            var gameDetailsFromApi = (JsonConvert.DeserializeObject<List<ApiGame>>(gameJsonResponse) ?? new List<ApiGame>()).FirstOrDefault();

            if (gameDetailsFromApi == null) return NotFound();

            bool isInLibrary = false;
            // 2. Sprawdź, czy gra jest w bibliotece (tylko jeśli user jest zalogowany)
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var currentUserId = GetCurrentUserId();
                if (currentUserId != Guid.Empty)
                {
                    isInLibrary = await _context.GamesInLibraries
                        .AnyAsync(g => g.UserId == currentUserId && g.IgdbGameId == gameId);
                }
            }

            // 3. Pobierz poradniki dla tej gry z bazy danych
            // (Na razie lista będzie pusta, dopóki ich nie dodamy)
            var guidesFromDb = await _context.Guides
                .Where(g => g.IgdbGameId == gameId)
                .ToListAsync(); // W przyszłości można dodać .Include(g => g.Tips)

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