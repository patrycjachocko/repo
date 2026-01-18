using System.ComponentModel.DataAnnotations;

namespace praca_dyplomowa_zesp.Models.ViewModels.Users
{
    public class RegisterViewModel //model danych do walidacji rejestracji nowego użytkownika
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