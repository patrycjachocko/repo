using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using praca_dyplomowa_zesp.Models.Modules.Users;

namespace praca_dyplomowa_zesp.Models.Modules.Libraries
{
    public class GameInLibrary //model reprezentujący grę przypisaną do osobistej biblioteki użytkownika
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public long IgdbGameId { get; set; }

        public Guid UserId { get; set; }
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        public DateTime DateAddedToLibrary { get; set; }

        public string? CurrentUserStoryMission { get; set; }

        public int CurrentUserStoryProgressPercent { get; set; } //procent ukończenia gry

        public string? Notes { get; set; }

        public virtual ICollection<ToDoItem> ToDoItems { get; set; } = new List<ToDoItem>(); //lista zadań

        public DateTime LastAccessed { get; set; } //data ostatniej interakcji z grą w bibliotece
    }
}