/// <summary>
/// Reprezentuje jedno osiągnięcie, łącząc jego dane z IGDB ze stanem odblokowania przez gracza.
/// </summary>
public class AchievementViewModel
{
    /// <summary>
    /// Nazwa osiągnięcia (np. "Wędrowiec").
    /// POBIERANE Z: IGDB
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Opis, co należy zrobić, aby zdobyć osiągnięcie. Pokazywany po najechaniu.
    /// POBIERANE Z: IGDB
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// URL do kolorowej ikony osiągnięcia.
    /// POBIERANE Z: IGDB
    /// </summary>
    public string IconUrl { get; set; }

    /// <summary>
    /// Status odblokowania przez gracza. Kluczowy do określenia wyglądu (kolor/czarno-biały).
    /// ZAPISYWANE W: Twojej bazie
    /// </summary>
    public bool IsUnlocked { get; set; }
}