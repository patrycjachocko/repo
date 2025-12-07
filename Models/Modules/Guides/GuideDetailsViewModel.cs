using System.Collections.Generic;
using praca_dyplomowa_zesp.Models.Interactions.Comments;

namespace praca_dyplomowa_zesp.Models.Modules.Guides
{
    public class GuideDetailsViewModel
    {
        // Główny obiekt poradnika
        public Guide Guide { get; set; }

        // Lista komentarzy do tego poradnika
        public List<Comment> Comments { get; set; }

        // Ocena wystawiona przez aktualnie zalogowanego użytkownika (do wyświetlenia "Twoja ocena")
        public double UserRating { get; set; }

        // Średnia ocen wszystkich użytkowników
        public double AverageRating { get; set; }

        public GuideDetailsViewModel()
        {
            Comments = new List<Comment>();
        }
    }
}