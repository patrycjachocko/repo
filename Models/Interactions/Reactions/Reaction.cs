using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa_zesp.Models.Interactions.Reactions
{
    public class Reaction
    {
        public int Id { get; set; }
        public ReactionType Type { get; set; }
        public User User { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  
        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
