using praca_dyplomowa_zesp.Models.Users;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace praca_dyplomowa_zesp.Models.Modules.Guides
{
    public class GameMap
    {
        public int Id { get; set; }

        public long IgdbGameId { get; set; } // Przypisanie do gry

        [Required]
        public string Name { get; set; } // Nazwa mapy, np. "Velen - Południe"

        [Required]
        public string ImageUrl { get; set; } // Ścieżka do pliku na serwerze

        // Kto dodał mapę (opcjonalne, ale warto wiedzieć)
        public Guid? UploadedByUserId { get; set; }
        [ForeignKey("UploadedByUserId")]
        public User? UploadedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
    }
}