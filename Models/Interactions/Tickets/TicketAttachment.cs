using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace praca_dyplomowa_zesp.Models
{
    public class TicketAttachment
    {
        [Key]
        public int Id { get; set; }

        public byte[] FileContent { get; set; }
        public string ContentType { get; set; }
        public string FileName { get; set; }

        // Relacja do wiadomości
        public int TicketMessageId { get; set; }
        [ForeignKey("TicketMessageId")]
        public virtual TicketMessage TicketMessage { get; set; }
    }
}