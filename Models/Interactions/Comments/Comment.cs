using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Interactions.Reactions;
using praca_dyplomowa_zesp.Models.Modules.Guides;
using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Models.Interactions.Comments
{
    public class Comment
    {
        [Key]
        public Guid Id { get; set; }
        [Required]
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User Author { get; set; }
        public int GuideId { get; set; }
        [ForeignKey("GuideId")]
        public virtual Guide Guide { get; set; }
        public virtual ICollection<Reply> Replies { get; set; } = new List<Reply>();
        public virtual ICollection<Reaction> Reactions { get; set; } = new List<Reaction>();
    }
}