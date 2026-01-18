namespace praca_dyplomowa_zesp.Models.ViewModels.Users
{
    public class ProfileViewModel //model widoku służący do prezentacji publicznych danych profilowych użytkownika
    {
        public Guid UserId { get; set; }

        public string Username { get; set; }

        public string? SteamId { get; set; } //identyfikator powiązanego konta Steam wykorzystywany do synchronizacji gier

        public bool IsCreatedBySteam { get; set; } //flaga informująca, czy konto zostało założone za pośrednictwem zewnętrznego dostawcy (OpenID STEAM API)
    }
}