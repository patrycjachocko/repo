using praca_dyplomowa_zesp.Models.Modules.Libraries;

namespace praca_dyplomowa_zesp.Models.ViewModels.Libraries
{
    public class MainLibraryViewModel //model widoku reprezentujący pojedynczy element gry w zestawieniu głównej biblioteki
    {
        public int DbId { get; set; }

        public long IgdbGameId { get; set; }

        public string Name { get; set; }

        public string CoverUrl { get; set; }

        public List<string> Genres { get; set; }

        public string Developer { get; set; }

        public string ReleaseDate { get; set; }

        public List<AchievementViewModel> Achievements { get; set; } //lista osiągnięć powiązanych z danym tytułem (pobierane z API Steam)

        public string SystemRequirementsUrl { get; set; }

        public int ProgressPercent { get; set; } //wizualny wskaźnik ukończenia gry
    }
}