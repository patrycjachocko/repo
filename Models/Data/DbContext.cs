using praca_dyplomowa_zesp.Models.Interactions.Comments;
using praca_dyplomowa_zesp.Models.Users;
using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa_zesp.Models.Modules.Guides;
using praca_dyplomowa_zesp.Models.Modules.Guides.Tips;
using praca_dyplomowa_zesp.Models.Interactions.Comments.Replies;
using praca_dyplomowa_zesp.Models.Interactions.Rates;
using praca_dyplomowa_zesp.Models.Interactions.Reactions;

namespace praca_dyplomowa.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // --- Istniejące tabele ---
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Guide> Guides { get; set; } = null!;
        public DbSet<Tip> Tips { get; set; } = null!;
        public DbSet<Comment> Comments { get; set; } = null!;
        public DbSet<Reply> Replies { get; set; } = null!;
        public DbSet<Rate> Rates { get; set; } = null!;
        public DbSet<Reaction> Reactions { get; set; } = null!;

        // --- NOWE TABELE DODANE DO BAZY ---
        public DbSet<GameInLibrary> GamesInLibraries { get; set; } = null!;
        public DbSet<UserAchievement> UserAchievements { get; set; } = null!;
    }
}