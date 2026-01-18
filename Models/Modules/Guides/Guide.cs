using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Interactions.Comments;
using praca_dyplomowa_zesp.Models.Interactions.Rates;
using praca_dyplomowa_zesp.Models.Modules.Users;

namespace praca_dyplomowa_zesp.Models.Modules.Guides
{
    public class Guide //główny model poradnika tworzonego przez społeczność graczy
    {
        [Key]
        public int Id { get; set; }

        public long IgdbGameId { get; set; } //powiązanie z konkretnym tytułem w bazie IGDB

        [Required(ErrorMessage = "Tytuł jest wymagany")]
        [StringLength(200, MinimumLength = 3)]
        public string Title { get; set; }

        [Required(ErrorMessage = "Treść jest wymagana")]
        public string Content { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public byte[]? CoverImage { get; set; } //dane binarne miniatury poradnika
        public string? CoverImageContentType { get; set; } //typ formatu graficznego okładki

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        public virtual ICollection<Rate> Rates { get; set; } = new List<Rate>();
        public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

        public bool IsApproved { get; set; } = false; //status zatwierdzenia przez moderatora
        public bool IsRejected { get; set; } = false; //flaga odrzucenia treści po weryfikacji
        public string? RejectionReason { get; set; } //uzasadnienie decyzji o odrzuceniu poradnika

        public bool IsDraft { get; set; } //oznaczenie wersji roboczej niewidocznej publicznie

        public bool IsDeleted { get; set; } = false; //znacznik miękkiego usunięcia z widoku publicznego
        public DateTime? DeletedAt { get; set; } //data przeniesienia do kosza
    }
}