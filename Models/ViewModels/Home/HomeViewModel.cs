using praca_dyplomowa_zesp.Models.Modules.Home;

namespace praca_dyplomowa_zesp.Models.ViewModels.Home
{
    public class HomeViewModel //model widoku strony głównej agregujący listę gier do wyświetlenia w sekcji promowanej
    {
        public List<HomeGameDisplay> Games { get; set; } = new List<HomeGameDisplay>(); //kolekcja gier prezentowanych na swiperze
    }
}