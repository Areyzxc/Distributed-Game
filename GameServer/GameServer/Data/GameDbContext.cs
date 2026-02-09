using Microsoft.EntityFrameworkCore;
using GameServer.Models;

namespace GameServer.Data
{
    public class GameDbContext : DbContext
    {
        public GameDbContext(DbContextOptions<GameDbContext> options) : base(options) { }

        public DbSet<Player> Players { get; set; }
        public DbSet<GameScore> Scores { get; set; }
        public DbSet<BannedPlayer> BannedPlayers { get; set; }
        public DbSet<MovementPattern> MovementPatterns { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Player relationships
            modelBuilder.Entity<Player>()
                .HasMany(p => p.Scores)
                .WithOne(s => s.Player)
                .HasForeignKey(s => s.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Player>()
                .HasMany(p => p.Bans)
                .WithOne(b => b.Player)
                .HasForeignKey(b => b.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Player>()
                .HasMany(p => p.MovementPatterns)
                .WithOne(m => m.Player)
                .HasForeignKey(m => m.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure unique constraints
            modelBuilder.Entity<Player>()
                .HasIndex(p => p.Username)
                .IsUnique();

            modelBuilder.Entity<Player>()
                .HasIndex(p => p.Email)
                .IsUnique();

            modelBuilder.Entity<BannedPlayer>()
                .HasIndex(b => b.PlayerId)
                .IsUnique();
        }
    }
}
