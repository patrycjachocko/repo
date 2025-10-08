using System.ComponentModel.DataAnnotations;

namespace praca_dyplomowa_zesp.Models.Users
{
    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(16)]
        public string Login { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        public string NormalizedEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        [Display(Name = "Password")]
        [RegularExpression(@"^[a-zA-Z0-9]+$", ErrorMessage = "Password must be alphanumeric.")]
        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
        [Compare("ConfirmPassword", ErrorMessage = "Passwords do not match.")]

        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "User"; // Default role is "User"

        public bool IsEmailConfirmed { get; set; } = false;
        [MaxLength(500)]
        public string? EmailConfirmationToken { get; set; }
        public DateTime? EmailConfirmationTokenCreatedAt { get; set; }
        public bool isBanned { get; set; } = false;
        public DateTimeOffset BanEnd { get; set; }
        public string? BanReason { get; set; }
        [MaxLength(500)]
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenCreatedAt { get; set; }
        public bool TwoFactorEnabled { get; set; } = false;
        public string? UserName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastActive { get; set; }
        public byte[]? ProfilePicture { get; set; } //podmienic na deafultowy
        public string? ProfilePictureContentType { get; set; }
        public byte[]? Banner { get; set; } //podmienic na deafultowy
        public string? BannerContentType { get; set; }

    }
}
