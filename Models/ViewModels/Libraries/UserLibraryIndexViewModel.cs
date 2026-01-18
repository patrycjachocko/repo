using System.Collections.Generic;

namespace praca_dyplomowa_zesp.Models.ViewModels.Libraries
{
    public class UserLibraryIndexViewModel
    {
        public List<MainLibraryViewModel> Games { get; set; } = new List<MainLibraryViewModel>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string SearchString { get; set; }
        public string StatusFilter { get; set; }
    }
}