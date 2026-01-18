using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Modules.Guides;
using praca_dyplomowa_zesp.Models.Modules.Users;

namespace praca_dyplomowa_zesp.Models.Interactions.Rates
{
    public class Rate //model ocen punktowych wystawianych poradnikom przez społeczność
    {
        [Key]
        public Guid Id { get; set; }

        [Range(1, 5)]
        public double Value { get; set; } //wartość oceny

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User User { get; set; } //powiązanie z kontem wystawiającego ocenę

        public int GuideId { get; set; }
        [ForeignKey("GuideId")]
        public virtual Guide Guide { get; set; }
    }
}