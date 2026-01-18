using praca_dyplomowa_zesp.Models.Modules.Guides;
using praca_dyplomowa_zesp.Models.Modules.Users;

namespace praca_dyplomowa_zesp.Models.ViewModels.Admin
{
    public class AdminPanelViewModel //zbiorczy model danych dla głównego widoku panelu administracyjnego
    {
        public List<AdminUserDto> Users { get; set; } = new List<AdminUserDto>(); //lista uproszczonych profili użytkowników do zarządzania

        public string SearchString { get; set; }

        public List<Guide> PendingGuides { get; set; } //poradniki oczekujące na weryfikację przez moderatora

        public List<Guide> DeletedGuides { get; set; } = new List<Guide>(); //archiwum usuniętych treści (kosz)

        public List<Ticket> Tickets { get; set; } = new List<Ticket>(); //lista aktywnych zgłoszeń od użytkowników
    }
}