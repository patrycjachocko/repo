using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Modules.Users;

namespace praca_dyplomowa_zesp.Models.Modules.Guides
{
    public class GameMap //model reprezentujący mapę graficzną przypisaną do konkretnej gry
    {
        [Key]
        public int Id { get; set; }

        public long IgdbGameId { get; set; } //powiązanie z gra z bazy IGDB

        [Required]
        public string Name { get; set; }

        [Required]
        public string ImageUrl { get; set; }

        public Guid? UploadedByUserId { get; set; }
        [ForeignKey("UploadedByUserId")]
        public virtual User? UploadedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsDeleted { get; set; } = false; //znacznik wykorzystywany przy miękkim usuwaniu rekordu
    }
}