using Newtonsoft.Json;
using System.Collections.Generic;

namespace praca_dyplomowa_zesp.Models.API
{
    public class ApiGame
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

        // --- Pola do logiki przekierowań i sortowania ---

        // 0 = Main Game, 1 = DLC, 3 = Bundle, etc.
        [JsonProperty("category")]
        public int Category { get; set; }

        [JsonProperty("parent_game")]
        public long? Parent_game { get; set; }

        [JsonProperty("version_parent")]
        public long? Version_parent { get; set; }

        // Kolekcje, do których należy gra (np. seria "The Sims")
        [JsonProperty("collections")]
        public List<ApiCollection> Collections { get; set; }
        [JsonProperty("rating")]
        public double? Rating { get; set; } // Ocena użytkowników IGDB

        [JsonProperty("aggregated_rating")]
        public double? Aggregated_rating { get; set; } // Ocena krytyków

        [JsonProperty("total_rating")]
        public double? Total_rating { get; set; } // Średnia ogólna
    }

    public class ApiCover { [JsonProperty("url")] public string Url { get; set; } }
    public class ApiGenre { [JsonProperty("name")] public string Name { get; set; } }
    public class ApiInvolvedCompany { [JsonProperty("company")] public ApiCompany Company { get; set; } [JsonProperty("developer")] public bool developer { get; set; } }
    public class ApiCompany { [JsonProperty("name")] public string Name { get; set; } }
    public class ApiReleaseDate { [JsonProperty("human")] public string Human { get; set; } }
    public class ApiWebsite { [JsonProperty("category")] public int Category { get; set; } [JsonProperty("url")] public string Url { get; set; } }
    public class ApiExternalGame { [JsonProperty("category")] public int Category { get; set; } [JsonProperty("uid")] public string Uid { get; set; } }

    // Nowa klasa dla Kolekcji
    public class ApiCollection { [JsonProperty("id")] public long Id { get; set; } [JsonProperty("name")] public string Name { get; set; } }

    public class ApiAchievement { public long Id { get; set; } public string Name { get; set; } public string Description { get; set; } public ApiAchievementIcon Achievement_icon { get; set; } }
    public class ApiAchievementIcon { public string Url { get; set; } }
}