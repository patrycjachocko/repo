namespace praca_dyplomowa_zesp.Models.Modules.Libraries
{
    public class AchievementViewModel //model widoku prezentujący szczegółowe dane o osiągnięciu w interfejsie
    {
        public string ExternalId { get; set; } //unikalny identyfikator osiągnięcia w systemie Steam

        public string Name { get; set; }

        public string Description { get; set; }

        public string IconUrl { get; set; } //adres URL do grafiki reprezentującej dane osiągnięcie

        public bool IsUnlocked { get; set; } //status określający, czy gracz zdobył już dane osiągnięcie
    }
}