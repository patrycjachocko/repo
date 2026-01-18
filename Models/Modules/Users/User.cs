using Microsoft.AspNetCore.Identity;
using System;
using System.ComponentModel.DataAnnotations;

namespace praca_dyplomowa_zesp.Models.Modules.Users
{
    public class User : IdentityUser<Guid>
    {
        [Required]
        [StringLength(16)]
        public string Login { get; set; } = string.Empty;

        public string Role { get; set; } = "User";

        public bool isBanned { get; set; } = false;
        public DateTimeOffset? BanEnd { get; set; }
        public string? BanReason { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? LastActive { get; set; }

        public byte[]? ProfilePicture { get; set; }
        public string? ProfilePictureContentType { get; set; }
        public string? SteamId { get; set; }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Login jest wymagany.")]
        [StringLength(16)]
        public string Login { get; set; }

        [Required(ErrorMessage = "Hasło jest wymagane.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Zapamiętaj mnie")]
        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Login jest wymagany.")]
        [StringLength(16, MinimumLength = 3, ErrorMessage = "Login musi mieć od 3 do 16 znaków.")]
        public string Login { get; set; }

        [Required(ErrorMessage = "Nazwa użytkownika jest wymagana.")]
        [Display(Name = "Nazwa użytkownika")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Hasło jest wymagane.")]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "{0} musi mieć co najmniej {2} i maksymalnie {1} znaków.", MinimumLength = 8)]
        [Display(Name = "Hasło")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Potwierdź hasło")]
        [Compare("Password", ErrorMessage = "Hasła nie są zgodne.")]
        public string ConfirmPassword { get; set; }
    }
}