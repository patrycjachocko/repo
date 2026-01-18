using praca_dyplomowa_zesp.Models.API;

namespace praca_dyplomowa_zesp.Models.ViewModels.Games
{
    public class GameBrowserViewModel //model widoku obsługujący listowanie gier oraz parametry wyszukiwania i filtrowania
    {
        public List<IGDBGameDtos> Games { get; set; } = new List<IGDBGameDtos>(); //kolekcja danych o grach pobrana z zewnętrznego API IGDB

        public int CurrentPage { get; set; } = 1;

        public string? SearchString { get; set; }

        public string Mode { get; set; } = "browse"; //tryb pracy przeglądarki

        public bool ShowIgdbUser { get; set; } = true;   //widoczność ocen wystawionych przez użytkowników serwisu IGDB
        public bool ShowIgdbCritic { get; set; } = true; //widoczność ocen pochodzących od profesjonalnych krytyków (IGDB)
        public bool ShowLocal { get; set; } = true;      //widoczność średniej ocen wystawionych przez społeczność GAMEHUB
    }
}