using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Models.Interactions.Comments.Replies
{
    public class Reply
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Relacja z Użytkownikiem (Autorem)
        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User Author { get; set; }

        // Relacja z Komentarzem (Rodzicem)
        public Guid ParentCommentId { get; set; }
        [ForeignKey("ParentCommentId")]
        public virtual Comment ParentComment { get; set; }
    }
}