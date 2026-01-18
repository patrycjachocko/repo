namespace praca_dyplomowa_zesp.Models.ViewModels.Libraries
{
    public class UserLibraryIndexViewModel //główny model widoku obsługujący wyświetlanie, wyszukiwanie i paginację osobistej kolekcji uzytkownika
    {
        public List<MainLibraryViewModel> Games { get; set; } = new List<MainLibraryViewModel>(); //kolekcja przetworzonych danych o grach w bibliotece

        // --- Parametry paginacji ---
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }

        // --- Parametry filtrowania ---
        public string SearchString { get; set; }
        public string StatusFilter { get; set; } //kryterium filtrowania gier ("Wszystkie", "Ukończone", "W trakcie", "Do zagrania")
    }
}