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



        /// <summary>
        /// Ta metoda jest wywoływana przez Entity Framework podczas tworzenia modelu bazy danych.
        /// Używamy jej do skonfigurowania początkowych danych (seeding).
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Definiujemy tutaj naszego testowego użytkownika, podając WSZYSTKIE wymagane wartości na sztywno
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = Guid.Parse("f2e330a6-5c93-4a7a-8b1a-952934c7a694"),
                    Login = "TestUser",
                    Email = "test@example.com",
                    PasswordHash = "dummy_hash_for_testing_purposes",
                    NormalizedEmail = "TEST@EXAMPLE.COM",
                    UserName = "Użytkownik Testowy",
                    // Dodajemy na sztywno datę, aby uniknąć dynamicznej wartości z konstruktora
                    CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );

            // Konfiguracja relacji pozostaje bez zmian
            modelBuilder.Entity<GameInLibrary>()
                .HasOne(g => g.User)
                .WithMany()
                .HasForeignKey(g => g.UserId);

            modelBuilder.Entity<UserAchievement>()
                .HasOne(ua => ua.User)
                .WithMany()
                .HasForeignKey(ua => ua.UserId);
        }



    }
}