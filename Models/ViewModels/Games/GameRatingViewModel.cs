namespace praca_dyplomowa_zesp.Models.ViewModels.Games
{
    public class GameRatingViewModel //model widoku agregujący oceny gry z różnych systemów i źródeł
    {
        public long IgdbGameId { get; set; }

        public double IgdbUserRating { get; set; } //średnia ocen społeczności IGDB
        public double IgdbCriticRating { get; set; } //średnia ocen krytyków IGDB

        public double LocalAverageRating { get; set; } //średnia wyliczona z ocen użytkowników GAMEHUB
        public int LocalRatingCount { get; set; } //całkowita liczba głosów oddanych lokalnie
        public double UserPersonalRating { get; set; } //ocena przypisana przez aktualnie zalogowanego użytkownika

        public double HybridTotalRating //właściwość obliczeniowa tworząca ujednolicony ranking (skala 0-100)
        {
            get
            {
                double sum = 0;
                int count = 0;

                //pobranie ocen z zewnętrznego systemu (IGDB zwraca wartości 0-100)
                if (IgdbUserRating > 0) { sum += IgdbUserRating; count++; }
                if (IgdbCriticRating > 0) { sum += IgdbCriticRating; count++; }

                //skalowanie oceny lokalnej (z 1-10 na 10-100) w celu ujednolicenia
                if (LocalAverageRating > 0) { sum += (LocalAverageRating * 10); count++; }

                return count > 0 ? sum / count : 0;
            }
        }
    }
}