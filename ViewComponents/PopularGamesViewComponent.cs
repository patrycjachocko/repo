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
            // ZMIANA: Używamy 'sort total_rating desc'. 
            // total_rating to średnia z ocen użytkowników (rating) i krytyków (aggregated_rating).
            // Dzięki temu dostaniemy "najlepsze" gry ogółem.
            string query = "fields name, cover.url, total_rating, category, version_parent; sort total_rating desc; where total_rating != null & cover.url != null & parent_game = null; limit 50;";

            List<IGDBGameDtos> games = new List<IGDBGameDtos>();

            try
            {
                var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);
                if (!string.IsNullOrEmpty(jsonResponse))
                {
                    games = JsonConvert.DeserializeObject<List<IGDBGameDtos>>(jsonResponse) ?? new List<IGDBGameDtos>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd w PopularGamesViewComponent: {ex.Message}");
            }

            // 2. Filtrowanie (Bez zmian - odrzucamy śmieci, bundle itp.)
            var filteredGames = games
                .Where(g =>
                    g.Category == 0 &&
                    g.Version_parent == null &&
                    !string.IsNullOrEmpty(g.Name) &&
                    !g.Name.Contains("Bundle", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.Contains("Collection", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.Contains("Anthology", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.EndsWith("Pack", StringComparison.OrdinalIgnoreCase)
                )
                .Take(10) // Bierzemy top 10 najlepszych
                .ToList();

            return View(filteredGames);
        }
    }
}