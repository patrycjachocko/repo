using Microsoft.AspNetCore.Mvc;
using praca_dyplomowa_zesp.Models;
using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using praca_dyplomowa_zesp.Models.Users;
using praca_dyplomowa.Data;
using praca_dyplomowa_zesp.Models.API;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using praca_dyplomowa_zesp.Models.Modules.Home;

namespace praca_dyplomowa_zesp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IGDBClient _igdbClient;
        private readonly UserManager<User> _userManager;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, IGDBClient igdbClient, UserManager<User> userManager)
        {
            _logger = logger;
            _context = context;
            _igdbClient = igdbClient;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new HomeViewModel();
            var finalGamesList = new List<HomeGameDisplay>();

            // Lista ID gier, które ju¿ dodaliœmy (¿eby nie dublowaæ popularnych z bibliotek¹)
            var addedIgdbIds = new HashSet<long>();

            // 1. SprawdŸ, czy u¿ytkownik jest zalogowany
            if (User.Identity.IsAuthenticated)
            {
                var userIdString = _userManager.GetUserId(User);
                if (Guid.TryParse(userIdString, out Guid userId))
                {
                    // Pobierz gry z biblioteki u¿ytkownika
                    var userLibraryGames = await _context.GamesInLibraries
                        .Where(g => g.UserId == userId)
                        .OrderByDescending(g => g.DateAddedToLibrary)
                        .Select(g => g.IgdbGameId)
                        .ToListAsync();

                    if (userLibraryGames.Any())
                    {
                        // Musimy pobraæ ok³adki i nazwy dla tych gier z IGDB
                        var idsString = string.Join(",", userLibraryGames);
                        var queryLibrary = $"fields name, cover.url; where id = ({idsString}); limit 50;";

                        var jsonLibrary = await _igdbClient.ApiRequestAsync("games", queryLibrary);
                        if (!string.IsNullOrEmpty(jsonLibrary))
                        {
                            var libraryGamesApi = JsonConvert.DeserializeObject<List<ApiGame>>(jsonLibrary);
                            if (libraryGamesApi != null)
                            {
                                foreach (var game in libraryGamesApi)
                                {
                                    finalGamesList.Add(new HomeGameDisplay
                                    {
                                        IgdbId = game.Id,
                                        Name = game.Name,
                                        CoverUrl = game.Cover?.Url?.Replace("t_thumb", "t_720p"), // Wysoka jakoœæ ok³adki
                                        IsInLibrary = true
                                    });
                                    addedIgdbIds.Add(game.Id);
                                }
                            }
                        }
                    }
                }
            }

            // 2. Pobierz popularne gry z IGDB
            // ZMIANA: Zwiêkszono limit z 20 na 50, ¿eby by³o wiêcej kart
            var queryPopular = "fields name, cover.url, rating; sort rating desc; where rating != null & cover.url != null & parent_game = null & rating_count > 50; limit 50;";
            var jsonPopular = await _igdbClient.ApiRequestAsync("games", queryPopular);

            if (!string.IsNullOrEmpty(jsonPopular))
            {
                var popularGamesApi = JsonConvert.DeserializeObject<List<ApiGame>>(jsonPopular);
                if (popularGamesApi != null)
                {
                    foreach (var game in popularGamesApi)
                    {
                        // Dodaj tylko jeœli nie ma jej jeszcze na liœcie (nie jest w bibliotece u góry)
                        if (!addedIgdbIds.Contains(game.Id))
                        {
                            finalGamesList.Add(new HomeGameDisplay
                            {
                                IgdbId = game.Id,
                                Name = game.Name,
                                CoverUrl = game.Cover?.Url?.Replace("t_thumb", "t_720p"),
                                IsInLibrary = false
                            });
                        }
                    }
                }
            }

            viewModel.Games = finalGamesList;
            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}