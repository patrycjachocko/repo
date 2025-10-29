﻿using System.Collections.Generic;
using System.Reflection.Emit;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using praca_dyplomowa_zesp.Models.Interactions.Comments;
using praca_dyplomowa_zesp.Models.Interactions.Comments.Replies;
using praca_dyplomowa_zesp.Models.Interactions.Rates;
using praca_dyplomowa_zesp.Models.Interactions.Reactions;
using praca_dyplomowa_zesp.Models.Modules.Guides;
using praca_dyplomowa_zesp.Models.Modules.Guides.Tips;
using praca_dyplomowa_zesp.Models.Users;

namespace praca_dyplomowa.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
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



            // Konfiguracja relacji pozostaje bez zmian
            modelBuilder.Entity<GameInLibrary>()
                .HasOne(g => g.User)
                .WithMany()
                .HasForeignKey(g => g.UserId);

            modelBuilder.Entity<UserAchievement>()
                .HasOne(ua => ua.User)
                .WithMany()
                .HasForeignKey(ua => ua.UserId);

            // <-- DODANE: Konfiguracja dla nowego pola w Guide -->
            modelBuilder.Entity<Guide>()
                .HasIndex(g => g.IgdbGameId);
        }



    }
}