using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Net; // Potrzebne do WebUtility.UrlEncode

namespace praca_dyplomowa_zesp.Models.API
{
    public class SteamApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public SteamApiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Steam:ApiKey"];
        }

        public virtual async Task<List<SteamGameDto>> GetUserGamesAsync(string steamId)
        {
            var url = $"http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={_apiKey}&steamid={steamId}&include_appinfo=true&format=json";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return new List<SteamGameDto>();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SteamGamesResponse>(content);

                return result?.Response?.Games ?? new List<SteamGameDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI Error] GetUserGames: {ex.Message}");
                return new List<SteamGameDto>();
            }
        }

        public virtual async Task<List<SteamAchievementDto>> GetGameAchievementsAsync(string steamId, string appId)
        {
            var url = $"http://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v0001/?appid={appId}&key={_apiKey}&steamid={steamId}&l=english";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[SteamAPI Error] GetPlayerAchievements Code: {response.StatusCode}");
                    return new List<SteamAchievementDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SteamAchievementsResponse>(content);
                return result?.PlayerStats?.Achievements ?? new List<SteamAchievementDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI Error] GetGameAchievements: {ex.Message}");
                return new List<SteamAchievementDto>();
            }
        }

        public virtual async Task<List<SteamAchievementSchemaDto>> GetSchemaForGameAsync(string appId)
        {
            var url = $"http://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={_apiKey}&appid={appId}&l=english";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[SteamAPI Error] GetSchemaForGame Code: {response.StatusCode} dla AppId: {appId}");
                    return new List<SteamAchievementSchemaDto>();
                }

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SteamSchemaResponse>(content);

                return result?.GameParams?.AvailableGameStats?.Achievements ?? new List<SteamAchievementSchemaDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI Error] GetSchemaForGame: {ex.Message}");
                return new List<SteamAchievementSchemaDto>();
            }
        }

        // --- NOWA METODA: Wyszukiwanie "na siłę" w sklepie Steam po nazwie ---
        public virtual async Task<string> SearchAppIdAsync(string query)
        {
            if (string.IsNullOrEmpty(query)) return null;

            // Używamy publicznego API wyszukiwania sklepu Steam
            var encodedName = WebUtility.UrlEncode(query);
            var url = $"https://store.steampowered.com/api/storesearch/?term={encodedName}&l=english&cc=US";

            try
            {
                Console.WriteLine($"[SteamAPI Search] Szukam gry: {query}");
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SteamStoreSearchResponse>(content);

                if (result != null && result.Items != null && result.Items.Any())
                {
                    var bestMatch = result.Items.First();
                    Console.WriteLine($"[SteamAPI Search] Znaleziono: {bestMatch.Name} (ID: {bestMatch.Id})");
                    return bestMatch.Id.ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI Search Error] {ex.Message}");
            }
            return null;
        }

        public virtual async Task<SteamPlayerSummaryDto?> GetPlayerSummaryAsync(string steamId)
        {
            var url = $"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={_apiKey}&steamids={steamId}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SteamPlayerSummaryResponse>(content);

                return result?.Response?.Players?.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        public async Task<byte[]> DownloadAvatarAsync(string url)
        {
            try
            {
                return await _httpClient.GetByteArrayAsync(url);
            }
            catch
            {
                return null;
            }
        }
    }

    // --- DTO ---

    public class SteamGamesResponse { [JsonPropertyName("response")] public SteamGameListResponse Response { get; set; } }
    public class SteamGameListResponse { [JsonPropertyName("games")] public List<SteamGameDto> Games { get; set; } }
    public class SteamGameDto { [JsonPropertyName("appid")] public int AppId { get; set; } [JsonPropertyName("name")] public string Name { get; set; } [JsonPropertyName("img_icon_url")] public string ImgIconUrl { get; set; } [JsonPropertyName("playtime_forever")] public int PlaytimeForever { get; set; } }

    public class SteamAchievementsResponse { [JsonPropertyName("playerstats")] public SteamPlayerStats PlayerStats { get; set; } }
    public class SteamPlayerStats { [JsonPropertyName("achievements")] public List<SteamAchievementDto> Achievements { get; set; } }
    public class SteamAchievementDto { [JsonPropertyName("apiname")] public string ApiName { get; set; } [JsonPropertyName("achieved")] public int Achieved { get; set; } [JsonPropertyName("name")] public string Name { get; set; } [JsonPropertyName("description")] public string Description { get; set; } }

    public class SteamPlayerSummaryResponse { [JsonPropertyName("response")] public SteamPlayerSummaryList Response { get; set; } }
    public class SteamPlayerSummaryList { [JsonPropertyName("players")] public List<SteamPlayerSummaryDto> Players { get; set; } }
    public class SteamPlayerSummaryDto { [JsonPropertyName("steamid")] public string SteamId { get; set; } [JsonPropertyName("personaname")] public string PersonaName { get; set; } [JsonPropertyName("avatarfull")] public string AvatarFullUrl { get; set; } }

    public class SteamSchemaResponse { [JsonPropertyName("game")] public SteamSchemaGameParams GameParams { get; set; } }
    public class SteamSchemaGameParams { [JsonPropertyName("availableGameStats")] public SteamSchemaAvailableStats AvailableGameStats { get; set; } }
    public class SteamSchemaAvailableStats { [JsonPropertyName("achievements")] public List<SteamAchievementSchemaDto> Achievements { get; set; } }
    public class SteamAchievementSchemaDto
    {
        [JsonPropertyName("name")] public string ApiName { get; set; }
        [JsonPropertyName("displayName")] public string DisplayName { get; set; }
        [JsonPropertyName("description")] public string Description { get; set; }
        [JsonPropertyName("icon")] public string Icon { get; set; }
        [JsonPropertyName("icongray")] public string IconGray { get; set; }
    }

    // --- NOWE DTO DO WYSZUKIWANIA ---
    public class SteamStoreSearchResponse
    {
        [JsonPropertyName("items")]
        public List<SteamStoreItemDto> Items { get; set; }
    }

    public class SteamStoreItemDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}