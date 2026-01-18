using System;
using System.Collections.Generic;

public class MainLibraryViewModel
{
    /// <summary>
    /// ID wpisu z Twojej lokalnej bazy danych. Niezbędne do linków (Details, Edit, Delete).
    /// </summary>
    public int DbId { get; set; } // DODANA WŁAŚCIWOŚĆ

    public long IgdbGameId { get; set; }
    public string Name { get; set; }
    public string CoverUrl { get; set; }
    public List<string> Genres { get; set; }
    public string Developer { get; set; }
    public string ReleaseDate { get; set; }
    public List<AchievementViewModel> Achievements { get; set; }
    public string SystemRequirementsUrl { get; set; }
    public int ProgressPercent { get; set; }
}