using System;
using System.Collections.Generic;

public class GameInLibraryViewModel
{
    public int DbId { get; set; }
    public long IgdbGameId { get; set; }
    public Guid UserId { get; set; } // <-- DODANE BRAKUJĄCE POLE
    public string Name { get; set; }
    public string? CoverUrl { get; set; }
    public List<string>? Genres { get; set; }
    public string? Developer { get; set; }
    public string? ReleaseDate { get; set; }
    public DateTime DateAddedToLibrary { get; set; }
    public string? CurrentUserStoryMission { get; set; }
    public int CurrentUserStoryProgressPercent { get; set; }
    public List<AchievementViewModel>? Achievements { get; set; }
}