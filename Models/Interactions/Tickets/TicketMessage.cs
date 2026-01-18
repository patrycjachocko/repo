using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Modules.Users;

namespace praca_dyplomowa_zesp.Models
{
    public class TicketMessage //model reprezentujący pojedynczą wiadomość wewnątrz wątku zgłoszenia
    {
        [Key]
        public int Id { get; set; }

        public int TicketId { get; set; }
        [ForeignKey("TicketId")]
        public virtual Ticket Ticket { get; set; }

        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [Required]
        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsStaffReply { get; set; } = false; //oznaczenie czy odpowiedź pochodzi od administratora/moderatora

        public virtual ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>(); //lista plików przypisanych do konkretnej wypowiedzi
    }
}