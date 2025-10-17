public class AchievementViewModel
{
    /// <summary>
    /// Unikalne ID osiągnięcia z bazy IGDB. Niezbędne do identyfikacji przy zapisie.
    /// </summary>
    public string ExternalId { get; set; } // DODANA WŁAŚCIWOŚĆ

    public string Name { get; set; }
    public string Description { get; set; }
    public string IconUrl { get; set; }
    public bool IsUnlocked { get; set; }
}