using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Modules.Users;

namespace praca_dyplomowa_zesp.Models.Interactions.Rates
{
    public class GameRate //model przechowujący oceny gier przypisane do konkretnych użytkowników
    {
        [Key]
        public int Id { get; set; }

        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        public long IgdbGameId { get; set; } //identyfikator produkcji w systemie IGDB

        public double Value { get; set; } //wartość punktowa (skala 1-10)

        public DateTime CreatedAt { get; set; }
    }
}