using System.ComponentModel.DataAnnotations;

namespace praca_dyplomowa_zesp.Models.ViewModels.Users
{
    public class LoginViewModel //model danych wykorzystywany w formularzu logowania
    {
        [Required(ErrorMessage = "Login jest wymagany.")]
        [StringLength(16)]
        public string Login { get; set; }

        [Required(ErrorMessage = "Hasło jest wymagane.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Zapamiętaj mnie")]
        public bool RememberMe { get; set; } //obsługa ciasteczek sesji
    }
}