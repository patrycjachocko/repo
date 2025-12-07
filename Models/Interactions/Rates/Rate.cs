using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Modules.Guides;
using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Models.Interactions.Rates
{
    public class Rate
    {
        [Key]
        public Guid Id { get; set; }

        [Range(1, 5)]
        public double Value { get; set; } // Zmieniono na double, aby obsługiwać 3.5, 4.5 itp.

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Relacja z Użytkownikiem
        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        // Relacja z Poradnikiem
        public int GuideId { get; set; }
        [ForeignKey("GuideId")]
        public virtual Guide Guide { get; set; }
    }
}