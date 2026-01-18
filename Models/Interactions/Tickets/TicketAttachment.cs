using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace praca_dyplomowa_zesp.Models
{
    public class TicketAttachment //model przechowujący załączniki binarne przesyłane w wiadomościach zgłoszeń
    {
        [Key]
        public int Id { get; set; }

        public byte[] FileContent { get; set; } //zawartość pliku w formacie binarnym
        public string ContentType { get; set; } //typ MIME pliku
        public string FileName { get; set; }

        public int TicketMessageId { get; set; }
        [ForeignKey("TicketMessageId")]
        public virtual TicketMessage TicketMessage { get; set; } //powiązanie z konkretną wiadomością w wątku zgłoszenia
    }
}