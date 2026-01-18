using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace praca_dyplomowa_zesp.Models.Modules.Libraries
{
    public class ToDoItem //model reprezentujący pojedynczy punkt na liście zadań ("to-do") przypisanej do gry
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Content { get; set; } = string.Empty;

        public bool IsCompleted { get; set; } = false;

        public int GameInLibraryId { get; set; }
        [ForeignKey("GameInLibraryId")]
        public virtual GameInLibrary GameInLibrary { get; set; } = null!; //powiązanie z konkretną grą w bibliotece użytkownika
    }
}