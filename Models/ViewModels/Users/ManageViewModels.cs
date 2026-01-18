using System.ComponentModel.DataAnnotations;

namespace praca_dyplomowa_zesp.Models.ViewModels.Users
{
    public class ChangePasswordViewModel //model widoku obsługujący zmianę hasła przez użytkownika
    {
        [Required(ErrorMessage = "Obecne hasło jest wymagane.")]
        [DataType(DataType.Password)]
        [Display(Name = "Obecne hasło")]
        public string OldPassword { get; set; }

        [Required(ErrorMessage = "Nowe hasło jest wymagane.")]
        [StringLength(100, ErrorMessage = "{0} musi mieć co najmniej {2} i maksymalnie {1} znaków.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Nowe hasło")]
        public string NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Potwierdź nowe hasło")]
        [Compare("NewPassword", ErrorMessage = "Hasła nie są zgodne.")]
        public string ConfirmPassword { get; set; }
    }
}