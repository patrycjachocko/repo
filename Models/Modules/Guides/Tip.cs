using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Interactions.Reactions;
using praca_dyplomowa_zesp.Models.Modules.Users;

namespace praca_dyplomowa_zesp.Models.Modules.Guides
{
    public class Tip //model krótkiej wskazówki publikowanej przez społeczność graczy
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

        public virtual ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
    }
}