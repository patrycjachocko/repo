using praca_dyplomowa_zesp.Models.Interactions.Comments;
using praca_dyplomowa_zesp.Models.Modules.Guides;

namespace praca_dyplomowa_zesp.Models.ViewModels.Guides
{
    public class GuideDetailsViewModel //model widoku agregujący kompletne dane poradnika wraz z interakcjami społecznościowymi
    {
        public Guide Guide { get; set; } //główny obiekt zawierający treść i metadane poradnika

        public List<Comment> Comments { get; set; }

        public double UserRating { get; set; } //wartość oceny wystawionej przez aktualnie zalogowanego użytkownika

        public double AverageRating { get; set; } //średnia arytmetyczna wszystkich ocen wystawionych poradnikowi

        public GuideDetailsViewModel() //konstruktor inicjalizujący pustą listę komentarzy
        {
            Comments = new List<Comment>();
        }
    }
}