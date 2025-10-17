using System;
using System.Collections.Generic;

public class GameInLibraryViewModel
{
    /// <summary>
    /// ID wpisu z Twojej lokalnej bazy danych. Niezbędne do linków (Edit, Delete).
    /// </summary>
    public int DbId { get; set; } // DODANA WŁAŚCIWOŚĆ

    // --- Dane z API IGDB ---
    public long IgdbGameId { get; set; }
    public string Name { get; set; }
    public string CoverUrl { get; set; }
    public List<string> Genres { get; set; }
    public string Developer { get; set; }
    public string ReleaseDate { get; set; }
    public List<AchievementViewModel> Achievements { get; set; }
    public string SystemRequirementsUrl { get; set; }

    // --- Dane Użytkownika z Twojej Bazy Danych ---
    public DateTime DateAddedToLibrary { get; set; }
    public string CurrentUserStoryMission { get; set; }
    public int CurrentUserStoryProgressPercent { get; set; }
}