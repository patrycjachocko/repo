using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Models.Interactions.Rates
{
    public class GameRate
    {
        [Key]
        public int Id { get; set; }

        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        public long IgdbGameId { get; set; } // ID gry z IGDB

        public double Value { get; set; } // Ocena 1-5

        public DateTime CreatedAt { get; set; }
    }
}