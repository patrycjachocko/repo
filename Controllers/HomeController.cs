using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using praca_dyplomowa_zesp.Models.API;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using praca_dyplomowa_zesp.Data;
using praca_dyplomowa_zesp.Models.ViewModels.Home;
using praca_dyplomowa_zesp.Models.Modules.Users;
using praca_dyplomowa_zesp.Models.Modules.Home;
using praca_dyplomowa_zesp.Models.ViewModels;

namespace praca_dyplomowa_zesp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IGDBClient _igdbClient;
        private readonly UserManager<User> _userManager;

        public HomeController(ApplicationDbContext context, IGDBClient igdbClient, UserManager<User> userManager)
        {
            //przypisanie wstrzyknietych serwisow do pol prywatnych kontrolera
            _context = context;
            _igdbClient = igdbClient;
            _userManager = userManager;
        }

        #region Actions

        public async Task<IActionResult> Index()
        {
            var viewModel = new HomeViewModel();
            var finalGamesList = new List<HomeGameDisplay>();
            var addedIgdbIds = new HashSet<long>();

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                //pobranie gier nalezacych do biblioteki zalogowanego uzytkownika
                await AppendUserLibraryGames(finalGamesList, addedIgdbIds);
            }

            //uzupelnienie listy o globalnie popularne tytuly z zewnetrznego api
            await AppendPopularGames(finalGamesList, addedIgdbIds);

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
            //wygenerowanie modelu bledu z unikalnym identyfikatorem zadania dla celow diagnostycznych
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        #endregion

        #region Private Methods (Logic)

        private async Task AppendUserLibraryGames(List<HomeGameDisplay> gamesList, HashSet<long> addedIds)
        {
            var userIdString = _userManager.GetUserId(User);
            if (Guid.TryParse(userIdString, out Guid userId))
            {
                //wyciagniecie z bazy id gier IGDB przypisanych do konta
                var userLibraryIds = await _context.GamesInLibraries
                    .Where(g => g.UserId == userId)
                    .OrderByDescending(g => g.DateAddedToLibrary)
                    .Select(g => g.IgdbGameId)
                    .ToListAsync();

                if (userLibraryIds.Any())
                {
                    string idsQuery = string.Join(",", userLibraryIds);
                    string query = $"fields name, cover.url; where id = ({idsQuery}); limit 50;";

                    var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);
                    if (!string.IsNullOrEmpty(jsonResponse))
                    {
                        var apiGames = JsonConvert.DeserializeObject<List<IGDBGameDtos>>(jsonResponse);
                        if (apiGames != null)
                        {
                            foreach (var game in apiGames)
                            {
                                //dodanie gry do listy glownej i oznaczenie jej jako posiadanej
                                gamesList.Add(MapToHomeDisplay(game, true));
                                addedIds.Add(game.Id);
                            }
                        }
                    }
                }
            }
        }

        private async Task AppendPopularGames(List<HomeGameDisplay> gamesList, HashSet<long> addedIds)
        {
            //pobranie najlepiej ocenianych gier z bazy igdb spelniajacych kryteria wiarygodnosci
            string query = "fields name, cover.url, rating, category, version_parent; sort rating desc; " +
                           "where rating != null & cover.url != null & parent_game = null & rating_count > 50; limit 100;";

            var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);
            if (!string.IsNullOrEmpty(jsonResponse))
            {
                var popularGames = JsonConvert.DeserializeObject<List<IGDBGameDtos>>(jsonResponse);
                if (popularGames != null)
                {
                    //reczne filtrowanie dlc i pakietow w pamieci serwera
                    var filtered = popularGames.Where(g =>
                        g.Category == 0 &&
                        g.Version_parent == null &&
                        !string.IsNullOrEmpty(g.Name) &&
                        !g.Name.Contains("Bundle", StringComparison.OrdinalIgnoreCase) &&
                        !g.Name.Contains("Collection", StringComparison.OrdinalIgnoreCase) &&
                        !g.Name.Contains("Anthology", StringComparison.OrdinalIgnoreCase) &&
                        !g.Name.EndsWith("Pack", StringComparison.OrdinalIgnoreCase));

                    foreach (var game in filtered)
                    {
                        //zapewnienie braku duplikatow miedzy biblioteka a lista popularnych
                        if (!addedIds.Contains(game.Id) && gamesList.Count < 30)
                        {
                            gamesList.Add(MapToHomeDisplay(game, false));
                            addedIds.Add(game.Id);
                        }
                    }
                }
            }
        }

        private HomeGameDisplay MapToHomeDisplay(IGDBGameDtos apiGame, bool isInLibrary)
        {
            return new HomeGameDisplay
            {
                IgdbId = apiGame.Id,
                Name = apiGame.Name,
                //podmiana parametru w adresie url okladki w celu uzyskania wyzszej jakosci
                CoverUrl = apiGame.Cover?.Url?.Replace("t_thumb", "t_720p"),
                IsInLibrary = isInLibrary
            };
        }

        #endregion
    }
}