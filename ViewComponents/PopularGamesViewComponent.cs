using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using praca_dyplomowa_zesp.Models.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace praca_dyplomowa_zesp.ViewComponents
{
    public class PopularGamesViewComponent : ViewComponent
    {
        private readonly IGDBClient _igdbClient;

        public PopularGamesViewComponent(IGDBClient igdbClient)
        {
            _igdbClient = igdbClient;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // 1. Zapytanie do API
            // Pobieramy więcej gier (limit 50), żeby mieć zapas po odrzuceniu śmieci (bundli, dlc itp.)
            // Sortujemy po 'rating' (ocenach), żeby pokazać najlepsze gry
            string query = "fields name, cover.url, rating, category, version_parent; sort rating desc; where rating != null & cover.url != null & parent_game = null; limit 50;";

            List<ApiGame> games = new List<ApiGame>();

            try
            {
                var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    games = JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse);
                }
            }
            catch (Exception ex)
            {
                // W razie błędu API nie wywalamy całej strony, tylko logujemy błąd (opcjonalnie)
                Console.WriteLine($"Błąd w PopularGamesViewComponent: {ex.Message}");
            }

            // 2. Filtrowanie (To samo co w GamesController)
            var filteredGames = games
                .Where(g =>
                    g.Category == 0 && // Tylko główne gry
                    g.Version_parent == null && // Bez Legacy Edition itp.
                    !string.IsNullOrEmpty(g.Name) &&
                    !g.Name.Contains("Bundle", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.Contains("Collection", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.Contains("Anthology", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.EndsWith("Pack", StringComparison.OrdinalIgnoreCase)
                )
                .Take(10) // Bierzemy top 10 już po przefiltrowaniu
                .ToList();

            return View(filteredGames);
        }
    }
}