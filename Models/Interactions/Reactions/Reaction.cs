using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Interactions.Comments;
using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Models.Interactions.Reactions
{
    public class Reaction
    {
        [Key]
        public Guid Id { get; set; }

        public ReactionType Type { get; set; }

        // Relacja z Użytkownikiem
        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User Author { get; set; }

        // Relacja z Komentarzem
        public Guid CommentId { get; set; }
        [ForeignKey("CommentId")]
        public virtual Comment Comment { get; set; }
    }
}