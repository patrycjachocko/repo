using praca_dyplomowa_zesp.Models.Users;
using System.ComponentModel.DataAnnotations;

namespace praca_dyplomowa_zesp.Models.Modules.Guides
{
    public class Guide
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public long IgdbGameId { get; set; } // <-- DODANE: Powiązanie z grą z API

        [Required]
        public string Title { get; set; }
        public string? Description { get; set; }

        public Guid UserId { get; set; } // ZMIANA Z INT NA GUID
        public virtual User User { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}