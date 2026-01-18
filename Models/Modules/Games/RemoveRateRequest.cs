namespace praca_dyplomowa_zesp.Models.Modules.Games
{
    public class RemoveRateRequest //model pomocniczy służący do przesyłania żądania usunięcia oceny gry
    {
        public long IgdbGameId { get; set; } //identyfikator gry w systemie IGDB, której ocena ma zostać usunięta
    }
}