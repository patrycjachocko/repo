using praca_dyplomowa_zesp.Models.Users;
using System;
using System.ComponentModel.DataAnnotations;

public class GameInLibrary
{
    [Key]
    public int Id { get; set; }

    [Required]
    public long IgdbGameId { get; set; }

    public Guid UserId { get; set; }
    public virtual User User { get; set; }

    public DateTime DateAddedToLibrary { get; set; }
    public string? CurrentUserStoryMission { get; set; }
    public int CurrentUserStoryProgressPercent { get; set; }

    public string? Notes { get; set; } // <-- DODANE NOWE POLE
}