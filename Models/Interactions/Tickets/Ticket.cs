using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Models
{
    public class Ticket
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Temat jest wymagany")]
        [StringLength(100, ErrorMessage = "Temat może mieć max 100 znaków")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Treść zgłoszenia jest wymagana")]
        [StringLength(2000, ErrorMessage = "Treść jest zbyt długa")]
        public string Content { get; set; }

        public TicketStatus Status { get; set; } = TicketStatus.Oczekujące;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ClosedAt { get; set; }

        // Powiązanie z użytkownikiem
        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
        public virtual ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
        public bool HasUnreadMessage { get; set; } = false;
        public bool HasUnreadResponse { get; set; } = false;
    }
}