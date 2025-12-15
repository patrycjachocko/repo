using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Models
{
    public class TicketMessage
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

        // Czy to wiadomość od pracownika (Admin/Mod)?
        public bool IsStaffReply { get; set; } = false;
        public virtual ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
    }
}