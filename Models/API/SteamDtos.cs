using System.Text.Json.Serialization;

namespace praca_dyplomowa_zesp.Models.API
{
    // --- modele danych odpowiedzi api steam ---

    public class SteamGamesResponse { [JsonPropertyName("response")] public SteamGameListResponse Response { get; set; } }

    public class SteamGameListResponse { [JsonPropertyName("games")] public List<SteamGameDto> Games { get; set; } }

    public class SteamGameDto
    {
        [JsonPropertyName("appid")] public int AppId { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("img_icon_url")] public string ImgIconUrl { get; set; }
        [JsonPropertyName("playtime_forever")] public int PlaytimeForever { get; set; } //całkowity czas gry wyrażony w minutach
    }

    public class SteamAchievementsResponse { [JsonPropertyName("playerstats")] public SteamPlayerStats PlayerStats { get; set; } }

    public class SteamPlayerStats { [JsonPropertyName("achievements")] public List<SteamAchievementDto> Achievements { get; set; } }

    public class SteamAchievementDto
    {
        [JsonPropertyName("apiname")] public string ApiName { get; set; }
        [JsonPropertyName("achieved")] public int Achieved { get; set; } //wartość 1 oznacza zdobycie osiągnięcia 0 brak
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("description")] public string Description { get; set; }
    }

    public class SteamPlayerSummaryResponse { [JsonPropertyName("response")] public SteamPlayerSummaryList Response { get; set; } }

    public class SteamPlayerSummaryList { [JsonPropertyName("players")] public List<SteamPlayerSummaryDto> Players { get; set; } }

    public class SteamPlayerSummaryDto
    {
        [JsonPropertyName("steamid")] public string SteamId { get; set; }
        [JsonPropertyName("personaname")] public string PersonaName { get; set; }
        [JsonPropertyName("avatarfull")] public string AvatarFullUrl { get; set; }
    }

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

    public class SteamStoreSearchResponse { [JsonPropertyName("items")] public List<SteamStoreItemDto> Items { get; set; } }

    public class SteamStoreItemDto
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }
    }
}