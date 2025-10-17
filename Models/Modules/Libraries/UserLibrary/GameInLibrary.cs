using praca_dyplomowa_zesp.Models.Users;
using System;
using System.ComponentModel.DataAnnotations;

public class GameInLibrary
{
    [Key]
    public int Id { get; set; }

    [Required]
    public long IgdbGameId { get; set; }

    /// <summary>
    /// Klucz obcy - musi być tego samego typu co User.Id, czyli Guid
    /// </summary>
    public Guid UserId { get; set; } // ZMIANA Z INT NA GUID
    public virtual User User { get; set; }

    public DateTime DateAddedToLibrary { get; set; }
    public string? CurrentUserStoryMission { get; set; }
    public int CurrentUserStoryProgressPercent { get; set; }
}