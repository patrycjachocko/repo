using praca_dyplomowa_zesp.Models.Users;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace praca_dyplomowa_zesp.Models.Interactions.Comments.Replies
{
    public class Reply
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;

        [Required]
        public Guid UserId { get; set; }

        public User User { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public Guid CommentId { get; set; }
        public Comment Comment { get; set; } = null!;
    }    
}
