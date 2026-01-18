using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Modules.Users;

namespace praca_dyplomowa_zesp.Models.Modules.Libraries
{
    public class UserAchievement //model rejestrujący fakt zdobycia konkretnego osiągnięcia przez gracza
    {
        [Key]
        public int Id { get; set; }

        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [Required]
        public long IgdbGameId { get; set; } //identyfikator gry, której dotyczy osiągnięcie

        [Required]
        public string AchievementExternalId { get; set; } //zewnętrzny identyfikator trofeum z Steam API

        public bool IsUnlocked { get; set; }
    }
}