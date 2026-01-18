using praca_dyplomowa_zesp.Models.Modules.Games;

namespace praca_dyplomowa_zesp.Models.ViewModels.Games
{
    public class GameDetailViewModel //model widoku agregujący szczegółowe informacje o grze pobrane z API IGDB oraz bazy lokalnej
    {
        public long IgdbGameId { get; set; }

        public string Name { get; set; }

        public string? CoverUrl { get; set; }

        public List<string> Genres { get; set; } = new List<string>(); //zbiór kategorii przypisanych do tytułu

        public string? Developer { get; set; }

        public string? ReleaseDate { get; set; }

        public GameRatingViewModel Ratings { get; set; } //podsumowanie ocen z różnych źródeł (Gracze IGDB, Krytycy IGDB, Społeczność GAMEHUB)

        public List<GameReview> TopReviews { get; set; } = new List<GameReview>(); //kolekcja najlepszych recenzji
    }
}