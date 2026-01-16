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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace praca_dyplomowa_zesp.Controllers
{
    /// <summary>
    /// Kontroler obs³uguj¹cy stronê g³ówn¹ aplikacji. 
    /// Odpowiada za wyœwietlanie gier z biblioteki u¿ytkownika oraz zestawienia popularnych tytu³ów.
    /// </summary>
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IGDBClient _igdbClient;
        private readonly UserManager<User> _userManager;

        public HomeController(ApplicationDbContext context, IGDBClient igdbClient, UserManager<User> userManager)
        {
            _context = context;
            _igdbClient = igdbClient;
            _userManager = userManager;
        }

        #region Actions

        /// <summary>
        /// Wyœwietla stronê g³ówn¹ z dynamicznie generowan¹ list¹ gier (Biblioteka + Popularne).
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var viewModel = new HomeViewModel();
            var finalGamesList = new List<HomeGameDisplay>();
            var addedIgdbIds = new HashSet<long>();

            // 1. Pobieranie gier z biblioteki zalogowanego u¿ytkownika
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                await AppendUserLibraryGames(finalGamesList, addedIgdbIds);
            }

            // 2. Pobieranie i filtrowanie popularnych gier z IGDB
            await AppendPopularGames(finalGamesList, addedIgdbIds);

            viewModel.Games = finalGamesList;
            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>
        /// Obs³uga b³êdów aplikacji z cache'owaniem wy³¹czonym dla precyzyjnej diagnostyki.
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        #endregion

        #region Private Methods (Logic)

        /// <summary>
        /// Pobiera identyfikatory gier z bazy danych u¿ytkownika i uzupe³nia ich metadane z API IGDB.
        /// </summary>
        private async Task AppendUserLibraryGames(List<HomeGameDisplay> gamesList, HashSet<long> addedIds)
        {
            var userIdString = _userManager.GetUserId(User);
            if (Guid.TryParse(userIdString, out Guid userId))
            {
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
                        var apiGames = JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse);
                        if (apiGames != null)
                        {
                            foreach (var game in apiGames)
                            {
                                gamesList.Add(MapToHomeDisplay(game, true));
                                addedIds.Add(game.Id);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Pobiera listê popularnych gier z IGDB, filtruj¹c dodatki (DLC) i pakiety (Bundles).
        /// </summary>
        private async Task AppendPopularGames(List<HomeGameDisplay> gamesList, HashSet<long> addedIds)
        {
            // Zapytanie o gry g³ówne (category 0) z wysok¹ ocen¹ i wymagan¹ ok³adk¹
            string query = "fields name, cover.url, rating, category, version_parent; sort rating desc; " +
                           "where rating != null & cover.url != null & parent_game = null & rating_count > 50; limit 100;";

            var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);
            if (!string.IsNullOrEmpty(jsonResponse))
            {
                var popularGames = JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse);
                if (popularGames != null)
                {
                    // Filtrowanie in-memory (kategoria, wersje legacy, s³owa kluczowe w nazwie)
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
                        // Dodajemy tylko unikalne gry (jeœli nie ma ich ju¿ z biblioteki) do limitu 30 sztuk
                        if (!addedIds.Contains(game.Id) && gamesList.Count < 30)
                        {
                            gamesList.Add(MapToHomeDisplay(game, false));
                            addedIds.Add(game.Id);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Mapuje obiekt z API IGDB na model wyœwietlania strony g³ównej.
        /// </summary>
        private HomeGameDisplay MapToHomeDisplay(ApiGame apiGame, bool isInLibrary)
        {
            return new HomeGameDisplay
            {
                IgdbId = apiGame.Id,
                Name = apiGame.Name,
                CoverUrl = apiGame.Cover?.Url?.Replace("t_thumb", "t_720p"),
                IsInLibrary = isInLibrary
            };
        }

        #endregion
    }
}