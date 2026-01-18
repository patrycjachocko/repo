using System.Text.Json;
using System.Net;
using praca_dyplomowa_zesp.Models.API;

namespace praca_dyplomowa_zesp.Services
{
    public class SteamApiService //serwis odpowiedzialny za komunikację z zewnętrznym STEAM API
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public SteamApiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Steam:ApiKey"];
        }

        public virtual async Task<List<SteamGameDto>> GetUserGamesAsync(string steamId) //pobieranie listy gier przypisanych do konta steam użytkownika
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

        public virtual async Task<List<SteamAchievementDto>> GetGameAchievementsAsync(string steamId, string appId) //pobieranie stanu osiągnięć gracza w wybranym tytule
        {
            var url = $"http://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v0001/?appid={appId}&key={_apiKey}&steamid={steamId}&l=english";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return new List<SteamAchievementDto>();

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

        public virtual async Task<List<SteamAchievementSchemaDto>> GetSchemaForGameAsync(string appId) //pobieranie nazw i ikon osiągnięć ze schematu gry
        {
            var url = $"http://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?key={_apiKey}&appid={appId}&l=english";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return new List<SteamAchievementSchemaDto>();

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

        public virtual async Task<string> SearchAppIdAsync(string query) //wyszukiwanie id gry w bazie steam na podstawie nazwy tekstowej
        {
            if (string.IsNullOrEmpty(query)) return null;

            var encodedName = WebUtility.UrlEncode(query);
            var url = $"https://store.steampowered.com/api/storesearch/?term={encodedName}&l=english&cc=US";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SteamStoreSearchResponse>(content);

                if (result?.Items != null && result.Items.Any())
                {
                    var bestMatch = result.Items.First(); //wybór pierwszego wyniku jako najbardziej pasującego
                    return bestMatch.Id.ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SteamAPI Search Error] {ex.Message}");
            }
            return null;
        }

        public virtual async Task<SteamPlayerSummaryDto?> GetPlayerSummaryAsync(string steamId) //pobieranie danych profilu publicznego gracza
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

        public async Task<byte[]> DownloadAvatarAsync(string url) //pobieranie grafiki awatara
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
}