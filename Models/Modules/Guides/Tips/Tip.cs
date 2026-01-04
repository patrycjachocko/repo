using System;
using System.Collections.Generic; // Potrzebne do ICollection
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Interactions.Reactions; // Using do Reaction
using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Models.Modules.Guides.Tips
{
    public class Tip
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(280)]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public long IgdbGameId { get; set; }

        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        // --- NOWOŚĆ: Lista reakcji ---
        public virtual ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
    }
}