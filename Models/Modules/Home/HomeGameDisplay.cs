namespace praca_dyplomowa_zesp.Models.Modules.Home
{
    public class HomeGameDisplay //uproszczony model danych gry przeznaczony do prezentacji w kafelkach na stronie głównej
    {
        public long IgdbId { get; set; }

        public string Name { get; set; }

        public string CoverUrl { get; set; }

        public bool IsInLibrary { get; set; } //znacznik informujący czy dana gra jest już w kolekcji zalogowanego użytkownika
    }
}