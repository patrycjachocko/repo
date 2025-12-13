using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Interactions.Comments;
using praca_dyplomowa_zesp.Models.Interactions.Rates;
using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Models.Modules.Guides
{
    public class Guide
    {
        [Key]
        public int Id { get; set; }

        public long IgdbGameId { get; set; }

        [Required(ErrorMessage = "Tytuł jest wymagany")]
        [StringLength(200, MinimumLength = 3)]
        public string Title { get; set; }

        [Required(ErrorMessage = "Treść jest wymagana")]
        public string Content { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public byte[]? CoverImage { get; set; }
        public string? CoverImageContentType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // Autor
        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        // Relacje dodane dla nowych funkcjonalności:
        public virtual ICollection<Rate> Rates { get; set; } = new List<Rate>();
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

        public bool IsApproved { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}