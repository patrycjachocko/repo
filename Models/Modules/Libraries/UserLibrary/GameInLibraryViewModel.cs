using praca_dyplomowa_zesp.Models.Modules.Libraries;
using System;
using System.Collections.Generic;

namespace praca_dyplomowa_zesp.Models.Modules.Libraries.UserLibrary
{
    public class GameInLibraryViewModel
    {
        public int DbId { get; set; }
        public long IgdbGameId { get; set; }
        public Guid UserId { get; set; }

        public string Name { get; set; }
        public string? CoverUrl { get; set; }
        public List<string> Genres { get; set; } = new List<string>();
        public string? Developer { get; set; }
        public string? ReleaseDate { get; set; }

        public DateTime DateAddedToLibrary { get; set; }
        public string? CurrentUserStoryMission { get; set; }
        public int CurrentUserStoryProgressPercent { get; set; }
        public string? Notes { get; set; }

        public List<AchievementViewModel> Achievements { get; set; } = new List<AchievementViewModel>();

        public bool IsSteamConnected { get; set; }
        public bool IsSteamGame { get; set; }

        // NOWE POLE: Lista ID osiągnięć, które są zdobyte na Steam (do sterowania widokiem)
        public List<string> SteamUnlockedAchievementIds { get; set; } = new List<string>();
        public List<ToDoItem> ToDoItems { get; set; } = new List<ToDoItem>();
    }
}