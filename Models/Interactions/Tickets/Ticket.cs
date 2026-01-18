using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Modules.Users;

namespace praca_dyplomowa_zesp.Models
{
    public class Ticket //model zgłoszenia przesyłanego do administracji
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Temat jest wymagany")]
        [StringLength(100, ErrorMessage = "Temat może mieć max 100 znaków")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Treść zgłoszenia jest wymagana")]
        [StringLength(2000, ErrorMessage = "Treść jest zbyt długa")]
        public string Content { get; set; }

        public TicketStatus Status { get; set; } = TicketStatus.Oczekujące; //aktualny etap realizacji zgłoszenia

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ClosedAt { get; set; }

        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User User { get; set; } //powiązanie z kontem zgłaszającego użytkownika

        public virtual ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>(); //historia korespondencji wewnątrz zgłoszenia

        public bool HasUnreadMessage { get; set; } = false; //flaga dla administratora o nowej wiadomości od użytkownika
        public bool HasUnreadResponse { get; set; } = false; //flaga dla użytkownika o nowej odpowiedzi od administracji
    }
}