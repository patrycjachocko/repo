using praca_dyplomowa_zesp.Models.Users;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Hosting;
using praca_dyplomowa_zesp.Models.Interactions.Comments.Replies;

namespace praca_dyplomowa_zesp.Models.Interactions.Comments
{
    public class Comment
    {
            [Key]
            public Guid Id { get; set; } = Guid.NewGuid();

            [Required]
            [MaxLength(2000)]
            public string Content { get; set; } = string.Empty;

            [Required]
            public Guid UserId { get; set; }

            public User User { get; set; } = null!;

            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
            public DateTime? UpdatedAt { get; set; }

            public ICollection<Reply> Replies { get; set; } = new List<Reply>();
    }
    
}