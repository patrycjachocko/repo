using System.ComponentModel.DataAnnotations;
using System;
using praca_dyplomowa_zesp.Models.Modules.Users; // Potrzebne dla Guid

public class UserAchievement
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// ID użytkownika - musi być tego samego typu co User.Id, czyli Guid
    /// </summary>
    public Guid UserId { get; set; } // ZMIANA Z INT NA GUID
    public virtual User User { get; set; }

    [Required]
    public long IgdbGameId { get; set; }

    [Required]
    public string AchievementExternalId { get; set; }

    public bool IsUnlocked { get; set; }
}