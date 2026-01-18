using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Interactions.Reactions;
using praca_dyplomowa_zesp.Models.Modules.Users;

namespace praca_dyplomowa_zesp.Models.Interactions.Comments
{
    public class Reply //model odpowiedzi wewnątrz wątku dyskusyjnego pod poradnikiem
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User Author { get; set; } //powiązanie z kontem twórcy odpowiedzi

        public Guid ParentCommentId { get; set; }
        [ForeignKey("ParentCommentId")]
        public virtual Comment ParentComment { get; set; } //odniesienie do komentarza nadrzędnego w strukturze

        public virtual ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
    }
}