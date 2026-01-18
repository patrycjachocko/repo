namespace praca_dyplomowa_zesp.Models.ViewModels.Home
{
    public class HomeViewModel
    {
        public List<HomeGameDisplay> Games { get; set; } = new List<HomeGameDisplay>();
    }

    public class HomeGameDisplay
    {
        public long IgdbId { get; set; }
        public string Name { get; set; }
        public string CoverUrl { get; set; }
        public bool IsInLibrary { get; set; }
    }
}