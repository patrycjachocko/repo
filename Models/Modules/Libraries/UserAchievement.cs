using praca_dyplomowa_zesp.Models.Users;
using System.ComponentModel.DataAnnotations;

public class UserAchievement
{
    [Key]
    public int Id { get; set; }

    // --- Połączenie z Użytkownikiem i grą z IGDB ---

    /// <summary>
    /// ID użytkownika, do którego należy to osiągnięcie.
    /// </summary>
    public int UserId { get; set; }
    public virtual User User { get; set; }

    /// <summary>
    /// ID gry z bazy IGDB, do której to osiągnięcie należy.
    /// </summary>
    [Required]
    public long IgdbGameId { get; set; }

    // --- Dane o osiągnięciu, które ZAPISUJEMY W BAZIE ---

    /// <summary>
    /// Unikalny identyfikator osiągnięcia z IGDB (np. jego nazwa lub ID, jeśli API je podaje).
    /// Używamy tego, by wiedzieć, o które osiągnięcie chodzi.
    /// </summary>
    [Required]
    public string AchievementExternalId { get; set; }

    /// <summary>
    /// Status odblokowania przez gracza.
    /// </summary>
    public bool IsUnlocked { get; set; }
}