using praca_dyplomowa_zesp.Models.Users;
using System;
using System.ComponentModel.DataAnnotations;

namespace praca_dyplomowa_zesp.Models.Modules.Guides
{
    public class Guide
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public long IgdbGameId { get; set; }

        [Required(ErrorMessage = "Tytuł jest wymagany.")]
        [Display(Name = "Tytuł poradnika")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Krótki opis (wstęp) jest wymagany.")]
        [Display(Name = "Wstęp (zajawka)")]
        [StringLength(300, ErrorMessage = "Wstęp nie może być dłuższy niż 300 znaków.")]
        public string Description { get; set; } // To będzie wyświetlane na kafelku

        [Display(Name = "Treść poradnika")]
        public string? Content { get; set; } // To jest pełna treść z edytora (HTML)

        // --- Obsługa zdjęcia okładkowego ---
        [Display(Name = "Zdjęcie okładkowe")]
        public byte[]? CoverImage { get; set; }
        public string? CoverImageContentType { get; set; }

        public Guid UserId { get; set; }
        public virtual User User { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}