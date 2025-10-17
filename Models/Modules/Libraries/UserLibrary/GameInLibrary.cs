using praca_dyplomowa_zesp.Models.Users;
using System;
using System.ComponentModel.DataAnnotations; // Ważne: dodaj tę przestrzeń nazw

public class GameInLibrary
{
    // --- Klucz główny dla bazy danych ---

    [Key] // Ten atrybut oznacza, że to jest klucz główny tabeli
    public int Id { get; set; }

    // --- Dane do połączenia z API i Użytkownikiem ---

    /// <summary>
    /// ID gry z bazy IGDB. To jest nasz łącznik z zewnętrznym API.
    /// </summary>
    [Required] // Oznacza, że to pole jest wymagane
    public long IgdbGameId { get; set; }

    /// <summary>
    /// Klucz obcy wskazujący, do którego użytkownika należy ten wpis.
    /// </summary>
    public int UserId { get; set; }
    public virtual User User { get; set; } // Właściwość nawigacyjna do obiektu User

    // --- Dane Użytkownika, które ZAPISUJEMY W BAZIE ---

    /// <summary>
    /// Data, kiedy użytkownik dodał grę do swojej biblioteki.
    /// </summary>
    public DateTime DateAddedToLibrary { get; set; }

    /// <summary>
    /// Nazwa ostatniej misji fabularnej, na której jest użytkownik.
    /// </summary>
    public string? CurrentUserStoryMission { get; set; } // Znak '?' oznacza, że pole może być puste (null)

    /// <summary>
    /// Procent ukończenia fabuły.
    /// </summary>
    public int CurrentUserStoryProgressPercent { get; set; }
}