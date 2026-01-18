using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace praca_dyplomowa_zesp.Models.Modules.Users
{
    public class User : IdentityUser<Guid> //rozszerzony model użytkownika systemu Identity
    {
        [Required]
        [StringLength(16)]
        public string Login { get; set; } = string.Empty;

        public string Role { get; set; } = "User"; //przypisana rola uprawnień

        public bool isBanned { get; set; } = false; //flaga blokady konta
        public DateTimeOffset? BanEnd { get; set; }
        public string? BanReason { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? LastActive { get; set; }

        public byte[]? ProfilePicture { get; set; }
        public string? ProfilePictureContentType { get; set; } //format graficzny awatara

        public string? SteamId { get; set; } //identyfikator do synchronizacji z platformą Steam
    }
}