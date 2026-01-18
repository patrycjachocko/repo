using praca_dyplomowa_zesp.Models.Modules.Guides;

namespace praca_dyplomowa_zesp.Models.ViewModels.Guides
{
    public class GuidesViewModel //model widoku agregujący poradniki i wskazówki przypisane do konkretnego tytułu
    {
        public long IgdbGameId { get; set; }

        public string GameName { get; set; } = "Nieznana gra";

        public bool IsInLibrary { get; set; } = false; //informacja czy gra znajduje się w kolekcji zalogowanego użytkownika

        public List<Guide> Guides { get; set; } = new List<Guide>(); //kolekcja poradników dla danej gry

        public IEnumerable<Tip> Tips { get; set; } = new List<Tip>(); //zbiór tipów dla danej gry
    }
}