using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Models.Interactions.Rates
{
    public class Rate
    {
        public int Id { get; set; }
        public int Value { get; set; } // 1-5 czy cos
        public User User { get; set; }
    }
}
