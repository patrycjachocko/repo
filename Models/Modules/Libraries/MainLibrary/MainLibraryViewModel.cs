using System;
using System.Collections.Generic;

/// <summary>
/// Model reprezentujący wszystkie informacje o grze z biblioteki użytkownika, 
/// gotowe do wyświetlenia na stronie. Łączy dane z IGDB i dane z Twojej bazy.
/// </summary>
public class MainLibraryViewModel
{
    // --- Dane z API IGDB ---

    /// <summary>
    /// ID gry z bazy IGDB. Niezbędne do dalszych zapytań.
    /// </summary>
    public long IgdbGameId { get; set; }

    /// <summary>
    /// Pełna nazwa gry.
    /// POBIERANE Z: IGDB
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// URL do kwadratowej okładki gry (np. w rozmiarze "cover_big").
    /// POBIERANE Z: IGDB
    /// </summary>
    public string CoverUrl { get; set; }

    /// <summary>
    /// Lista gatunków gry (np. "RPG", "Action").
    /// POBIERANE Z: IGDB
    /// </summary>
    public List<string> Genres { get; set; }

    /// <summary>
    /// Nazwa głównego studia deweloperskiego.
    /// POBIERANE Z: IGDB
    /// </summary>
    public string Developer { get; set; }

    /// <summary>
    /// Data wydania gry, sformatowana jako tekst (np. "19 maja 2015" lub "Q4 2025").
    /// POBIERANE Z: IGDB
    /// </summary>
    public string ReleaseDate { get; set; }

    /// <summary>
    /// Pełna lista osiągnięć dostępnych w grze, połączona ze stanem odblokowania przez gracza.
    /// DANE MIESZANE: Lista z IGDB + status "IsUnlocked" z Twojej bazy.
    /// </summary>
    public List<AchievementViewModel> Achievements { get; set; }

    /// <summary>
    /// Bezpośredni link do strony z oficjalnymi wymaganiami sprzętowymi (np. strona Steam lub wydawcy).
    /// POBIERANE Z: IGDB (z pola 'websites')
    /// </summary>
    public string SystemRequirementsUrl { get; set; }
}