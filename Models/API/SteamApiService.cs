using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration; // Potrzebne do IConfiguration

namespace praca_dyplomowa_zesp.Models.API // <--- ZMIANA TUTAJ (było .Services)
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

        public async Task<List<SteamGameDto>> GetUserGamesAsync(string steamId)
        {
            var url = $"http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={_apiKey}&steamid={steamId}&include_appinfo=true&format=json";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new List<SteamGameDto>();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SteamGamesResponse>(content);

            return result?.Response?.Games ?? new List<SteamGameDto>();
        }

        public async Task<List<SteamAchievementDto>> GetGameAchievementsAsync(string steamId, string appId)
        {
            var url = $"http://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v0001/?appid={appId}&key={_apiKey}&steamid={steamId}";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new List<SteamAchievementDto>();

            var content = await response.Content.ReadAsStringAsync();

            try
            {
                var result = JsonSerializer.Deserialize<SteamAchievementsResponse>(content);
                return result?.PlayerStats?.Achievements ?? new List<SteamAchievementDto>();
            }
            catch
            {
                return new List<SteamAchievementDto>();
            }
        }
    }

    // Klasy pomocnicze (DTO)

    public class SteamGamesResponse
    {
        [JsonPropertyName("response")]
        public SteamGameListResponse Response { get; set; }
    }

    public class SteamGameListResponse
    {
        [JsonPropertyName("games")]
        public List<SteamGameDto> Games { get; set; }
    }

    public class SteamGameDto
    {
        [JsonPropertyName("appid")]
        public int AppId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("img_icon_url")]
        public string ImgIconUrl { get; set; }

        [JsonPropertyName("playtime_forever")]
        public int PlaytimeForever { get; set; }
    }

    public class SteamAchievementsResponse
    {
        [JsonPropertyName("playerstats")]
        public SteamPlayerStats PlayerStats { get; set; }
    }

    public class SteamPlayerStats
    {
        [JsonPropertyName("achievements")]
        public List<SteamAchievementDto> Achievements { get; set; }
    }

    public class SteamAchievementDto
    {
        [JsonPropertyName("apiname")]
        public string ApiName { get; set; }

        [JsonPropertyName("achieved")]
        public int Achieved { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }
    }
}