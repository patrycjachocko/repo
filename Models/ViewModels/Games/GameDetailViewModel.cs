using System.Collections.Generic;
using praca_dyplomowa_zesp.Models.Modules.Games;

namespace praca_dyplomowa_zesp.Models.ViewModels.Games
{
    public class GameDetailViewModel
    {
        public long IgdbGameId { get; set; }
        public string Name { get; set; }
        public string? CoverUrl { get; set; }
        public List<string> Genres { get; set; } = new List<string>();
        public string? Developer { get; set; }
        public string? ReleaseDate { get; set; }
        public GameRatingViewModel Ratings { get; set; }

        // NOWE: Lista topowych recenzji do wyświetlenia
        public List<GameReview> TopReviews { get; set; } = new List<GameReview>();
    }
}