using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace praca_dyplomowa_zesp.Models.Modules.Libraries
{
    public class ToDoItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Content { get; set; } = string.Empty;

        public bool IsCompleted { get; set; } = false;

        // Relacja do gry w bibliotece
        public int GameInLibraryId { get; set; }

        [ForeignKey("GameInLibraryId")]
        public virtual GameInLibrary GameInLibrary { get; set; } = null!;
    }
}