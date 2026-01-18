using Newtonsoft.Json;

namespace praca_dyplomowa_zesp.Models.API
{
    public class IGDBGameDtos//model danych gry pobieranych z zewnętrznych api
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("cover")]
        public ApiCover Cover { get; set; }

        [JsonProperty("genres")]
        public List<ApiGenre> Genres { get; set; }

        [JsonProperty("involved_companies")]
        public List<ApiInvolvedCompany> Involved_companies { get; set; }

        [JsonProperty("release_dates")]
        public List<ApiReleaseDate> Release_dates { get; set; }

        [JsonProperty("websites")]
        public List<ApiWebsite> Websites { get; set; }

        [JsonProperty("external_games")]
        public List<ApiExternalGame> External_games { get; set; }

        [JsonProperty("category")]
        public int Category { get; set; } //typ gry np. podstawowa dlc lub zestaw

        [JsonProperty("parent_game")]
        public long? Parent_game { get; set; } //identyfikator gry nadrzędnej dla dlc lub dodatków

        [JsonProperty("version_parent")]
        public long? Version_parent { get; set; } //identyfikator pierwotnej wersji gry w przypadku edycji rozszerzonych

        [JsonProperty("collections")]
        public List<ApiCollection> Collections { get; set; } //seria do której należy dany tytuł

        [JsonProperty("rating")]
        public double? Rating { get; set; }

        [JsonProperty("aggregated_rating")]
        public double? Aggregated_rating { get; set; }

        [JsonProperty("total_rating")]
        public double? Total_rating { get; set; }
    }

    public class ApiCover
    {
        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public class ApiGenre
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class ApiInvolvedCompany
    {
        [JsonProperty("company")]
        public ApiCompany Company { get; set; }

        [JsonProperty("developer")]
        public bool developer { get; set; } //flaga określająca czy firma jest producentem gry
    }

    public class ApiCompany
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class ApiReleaseDate
    {
        [JsonProperty("human")]
        public string Human { get; set; }
    }

    public class ApiWebsite
    {
        [JsonProperty("category")]
        public int Category { get; set; } //typ strony internetowej np. oficjalna lub sklep

        [JsonProperty("url")]
        public string Url { get; set; }
    }

    public class ApiExternalGame
    {
        [JsonProperty("category")]
        public int Category { get; set; } //identyfikator platformy zewnętrznej np. steam

        [JsonProperty("uid")]
        public string Uid { get; set; }
    }

    public class ApiCollection
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class ApiAchievement
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ApiAchievementIcon Achievement_icon { get; set; }
    }

    public class ApiAchievementIcon
    {
        public string Url { get; set; }
    }
}