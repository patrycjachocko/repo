using praca_dyplomowa_zesp.Models.Modules.Libraries;
using praca_dyplomowa_zesp.Models.ViewModels.Games;

namespace praca_dyplomowa_zesp.Models.ViewModels.Libraries
{
    public class GameInLibraryViewModel //kompleksowy model widoku łączący dane techniczne gry z osobistymi postępami użytkownika
    {
        // --- Identyfikatory ---
        public int DbId { get; set; }
        public long IgdbGameId { get; set; }
        public Guid UserId { get; set; }

        // --- Dane pobrane z API (IGDB) ---
        public string Name { get; set; }
        public string? CoverUrl { get; set; }
        public List<string> Genres { get; set; } = new List<string>();
        public string? Developer { get; set; }
        public string? ReleaseDate { get; set; }

        // --- Postęp i dane użytkownika ---
        public DateTime DateAddedToLibrary { get; set; }
        public string? CurrentUserStoryMission { get; set; }
        public int CurrentUserStoryProgressPercent { get; set; } //procent ukończenia gry
        public string? Notes { get; set; }

        // --- Osiągnięcia i Integracja Steam ---
        public List<AchievementViewModel> Achievements { get; set; } = new List<AchievementViewModel>(); //lista wszystkich dostępnych osiągnięć
        public bool IsSteamConnected { get; set; } //status połączenia profilu z platformą Valve
        public bool IsSteamGame { get; set; } //flaga określająca, czy gra została zidentyfikowana w bibliotece Steam
        public List<string> SteamUnlockedAchievementIds { get; set; } = new List<string>(); //identyfikatory osiągnięć odblokowanych, pobrane bezpośrednio z API Steam

        // --- Elementy interaktywne ---
        public List<ToDoItem> ToDoItems { get; set; } = new List<ToDoItem>(); //lista zadań do zrobienia zdefiniowanych przez użytkownika
        public GameRatingViewModel Ratings { get; set; } //komponent zarządzający ocenami i rankingami
    }
}