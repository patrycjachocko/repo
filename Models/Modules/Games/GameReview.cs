using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Interactions.Reactions;
using praca_dyplomowa_zesp.Models.Modules.Users;

namespace praca_dyplomowa_zesp.Models.Modules.Games
{
    public class GameReview //model reprezentujący recenzję gry napisaną przez użytkownika
    {
        [Key]
        public int Id { get; set; }

        public long IgdbGameId { get; set; } //identyfikator IGDB

        [Required(ErrorMessage = "Treść recenzji jest wymagana")]
        [StringLength(3000, ErrorMessage = "Recenzja może mieć maksymalnie 3000 znaków")]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        public virtual ICollection<Reaction> Reactions { get; set; } = new List<Reaction>(); //lista ocen społeczności wystawionych pod recenzją
    }
}