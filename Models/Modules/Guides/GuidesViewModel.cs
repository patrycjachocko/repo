using praca_dyplomowa_zesp.Models.Modules.Guides;
using praca_dyplomowa_zesp.Models.Modules.Guides.Tips;
using System.Collections.Generic;

namespace praca_dyplomowa_zesp.Models.Modules.Guides
{
    public class GuidesViewModel
    {
        public long IgdbGameId { get; set; }
        public string GameName { get; set; } = "Nieznana gra";
        public bool IsInLibrary { get; set; } = false;

        // Lista poradników
        public List<Guide> Guides { get; set; } = new List<Guide>();
        public IEnumerable<Tip> Tips { get; set; } = new List<Tip>();
    }
}