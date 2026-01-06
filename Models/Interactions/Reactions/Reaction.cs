using praca_dyplomowa_zesp.Models.Interactions.Comments;
using praca_dyplomowa_zesp.Models.Interactions.Comments.Replies;
using praca_dyplomowa_zesp.Models.Modules.Guides.Tips;
using praca_dyplomowa_zesp.Models.Users;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Modules.Games;

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
        public virtual User User { get; set; }

        // Relacja z Komentarzem
        public Guid? CommentId { get; set; }
        [ForeignKey("CommentId")]
        public virtual Comment? Comment { get; set; }
        public Guid? ReplyId { get; set; }
        [ForeignKey("ReplyId")]
        public virtual Reply? Reply { get; set; }
        public int? TipId { get; set; }
        [ForeignKey("TipId")]
        public virtual Tip? Tip { get; set; }
        // W klasie Reaction dodaj:
        public int? GameReviewId { get; set; }
        [ForeignKey("GameReviewId")]
        public virtual GameReview? GameReview { get; set; }
    }
}