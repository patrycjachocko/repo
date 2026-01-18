using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using praca_dyplomowa_zesp.Models.API;

namespace praca_dyplomowa_zesp.ViewComponents
{
    public class PopularGamesViewComponent : ViewComponent //komponent odpowiedzialny za generowanie dynamicznej listy najlepiej ocenianych gier
    {
        private readonly IGDBClient _igdbClient;

        public PopularGamesViewComponent(IGDBClient igdbClient)
        {
            _igdbClient = igdbClient;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            //definicja zapytania: pobieramy gry z ocenami, odrzucając wersje alternatywne, bundle i dodatki
            //sortowanie po total_rating (średnia krytyków i graczy)
            string query = "fields name, cover.url, total_rating, category, version_parent; " +
                           "sort total_rating desc; " +
                           "where total_rating != null & cover.url != null & parent_game = null; " +
                           "limit 50;";

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
                //logowanie błędu komunikacji
                Console.WriteLine($"[PopularGamesViewComponent] Błąd API: {ex.Message}");
            }

            //zaawansowane filtrowanie wyników
            var filteredGames = games
                .Where(g =>
                    g.Category == 0 && //tylko główne wydania
                    g.Version_parent == null && //pomijamy edycje, które mają bazowego rodzica
                    !string.IsNullOrEmpty(g.Name) &&
                    !g.Name.Contains("Bundle", StringComparison.OrdinalIgnoreCase) && //eliminacja pakietów zbiorczych
                    !g.Name.Contains("Collection", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.Contains("Anthology", StringComparison.OrdinalIgnoreCase) &&
                    !g.Name.EndsWith("Pack", StringComparison.OrdinalIgnoreCase)
                )
                .Take(10) //finalny wybór top 10 produkcji do wyświetlenia w liście
                .ToList();

            return View(filteredGames);
        }
    }
}