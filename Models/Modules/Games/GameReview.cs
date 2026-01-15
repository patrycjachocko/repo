using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Interactions.Reactions;
using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Models.Modules.Games
{
    public class GameReview
    {
        [Key]
        public int Id { get; set; }

        public long IgdbGameId { get; set; } // ID gry z IGDB

        [Required(ErrorMessage = "Treść recenzji jest wymagana")]
        [StringLength(3000, ErrorMessage = "Recenzja może mieć maksymalnie 2000 znaków")]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Autor recenzji
        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        // Reakcje (Lajki/Dislajki)
        public virtual ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
    }
}