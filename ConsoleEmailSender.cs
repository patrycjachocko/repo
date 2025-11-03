using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;

namespace praca_dyplomowa_zesp
{
    public class ConsoleEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            Console.WriteLine("--- NOWY E-MAIL (DO CELÓW TESTOWYCH) ---");
            Console.WriteLine($"Do: {email}");
            Console.WriteLine($"Temat: {subject}");
            Console.WriteLine("Treść (skopiuj link do przeglądarki):");
            // W prawdziwej aplikacji, htmlMessage zawierałby tag <a>
            // Tutaj filtrujemy, by pokazać sam link
            Console.WriteLine(System.Text.RegularExpressions.Regex.Match(htmlMessage, @"href='(.*?)'").Groups[1].Value);
            Console.WriteLine("--------------------------------------");

            return Task.CompletedTask;
        }
    }
}