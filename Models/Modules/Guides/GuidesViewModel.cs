using System.Collections.Generic;
using praca_dyplomowa_zesp.Models.Modules.Guides;

namespace praca_dyplomowa_zesp.Models.Modules.Guides
{
    public class GuidesViewModel
    {
        public long IgdbGameId { get; set; }
        public string GameName { get; set; } = "Nieznana gra";
        public bool IsInLibrary { get; set; } = false;

        // Lista poradników
        public List<Guide> Guides { get; set; } = new List<Guide>();
    }
}