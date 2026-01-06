using System.Collections.Generic;
using praca_dyplomowa_zesp.Models.API;

namespace praca_dyplomowa_zesp.Models.Modules.Games
{
    public class GameBrowserViewModel
    {
        public List<ApiGame> Games { get; set; } = new List<ApiGame>();
        public int CurrentPage { get; set; } = 1;
        public string? SearchString { get; set; }
        public string Mode { get; set; } = "browse";
        public bool ShowIgdbUser { get; set; } = true;   // Gracze IGDB
        public bool ShowIgdbCritic { get; set; } = true; // Krytycy IGDB
        public bool ShowLocal { get; set; } = true;      // Twoja społeczność
    }
}