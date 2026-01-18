namespace praca_dyplomowa_zesp.Models.ViewModels.Games
{
    public class GameRatingViewModel
    {
        public long IgdbGameId { get; set; }

        public double IgdbUserRating { get; set; }
        public double IgdbCriticRating { get; set; }

        public double LocalAverageRating { get; set; }
        public int LocalRatingCount { get; set; }
        public double UserPersonalRating { get; set; }

        public double HybridTotalRating
        {
            get
            {
                double sum = 0;
                int count = 0;

                if (IgdbUserRating > 0) { sum += IgdbUserRating; count++; }
                if (IgdbCriticRating > 0) { sum += IgdbCriticRating; count++; }

                // ZMIANA: Skalujemy 10 -> 100 (mnożnik 10)
                if (LocalAverageRating > 0) { sum += (LocalAverageRating * 10); count++; }

                return count > 0 ? sum / count : 0;
            }
        }
    }
}