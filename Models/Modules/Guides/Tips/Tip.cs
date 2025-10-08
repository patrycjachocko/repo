using System.ComponentModel.DataAnnotations;

namespace praca_dyplomowa_zesp.Models.Modules.Guides.Tips
{
    public class Tip
    {
        [Key]
        public int Id { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Author { get; set; } //ściągać z usera nazwę czy odnośnik do usera? żeby wejść na profil
        public string Category { get; set; } //tytuł gry?
        public int Likes { get; set; }
        public string Tags { get; set; }
        public string? Version { get; set; } //wersja gry
    }
}
