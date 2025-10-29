using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory; // Dla IMemoryCache
using Newtonsoft.Json;
using praca_dyplomowa_zesp.Models.API; // Używamy modeli ApiGame
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace praca_dyplomowa_zesp.ViewComponents
{
    public class PopularGamesViewComponent : ViewComponent
    {
        private readonly IGDBClient _igdbClient;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "PopularGamesList";

        public PopularGamesViewComponent(IGDBClient igdbClient, IMemoryCache cache)
        {
            _igdbClient = igdbClient;
            _cache = cache;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Spróbuj pobrać listę z cache'a
            if (!_cache.TryGetValue(CacheKey, out List<ApiGame> popularGames))
            {
                // Jeśli w cache'u nic nie ma, pobierz z API
                var query = "fields name; sort rating desc; where rating != null & cover.url != null & parent_game = null; limit 5;";
                var jsonResponse = await _igdbClient.ApiRequestAsync("games", query);

                popularGames = string.IsNullOrEmpty(jsonResponse)
                    ? new List<ApiGame>()
                    : JsonConvert.DeserializeObject<List<ApiGame>>(jsonResponse) ?? new List<ApiGame>();

                // Ustaw opcje cache'a (ważność 1 godzina)
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromHours(1));

                // Zapisz listę w cache'u
                _cache.Set(CacheKey, popularGames, cacheEntryOptions);
            }

            // Zwróć widok z listą gier (z cache'a lub świeżo pobraną)
            return View(popularGames);
        }
    }
}