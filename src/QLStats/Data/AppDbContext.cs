using Microsoft.EntityFrameworkCore;
using QLStats.Data.Entities;

namespace QLStats.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<QLServer> QLServers => Set<QLServer>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<RoundResult> RoundResults => Set<RoundResult>();
    public DbSet<ZmqEvent> ZmqEvents => Set<ZmqEvent>();
    public DbSet<ScoringRule> ScoringRules => Set<ScoringRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Player>(e =>
        {
            e.HasIndex(p => p.SteamId).IsUnique();
            e.Property(p => p.SteamId).HasMaxLength(64);
            e.Property(p => p.DisplayName).HasMaxLength(64);
        });

        modelBuilder.Entity<QLServer>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(128);
            e.Property(s => s.ZmqAddress).HasMaxLength(256);
        });

        modelBuilder.Entity<Season>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(128);
        });

        modelBuilder.Entity<GameSession>(e =>
        {
            e.HasIndex(g => g.SessionDate);
            e.HasOne(g => g.Season)
                .WithMany(s => s.GameSessions)
                .HasForeignKey(g => g.SeasonId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Match>(e =>
        {
            e.HasIndex(m => m.MatchGuid).IsUnique();
            e.Property(m => m.MatchGuid).HasMaxLength(64);
            e.Property(m => m.Map).HasMaxLength(64);
            e.Property(m => m.GameType).HasMaxLength(32);
            e.Property(m => m.ServerTitle).HasMaxLength(128);
            e.HasIndex(m => m.StartedAt);
            e.HasOne(m => m.GameSession)
                .WithMany(s => s.Matches)
                .HasForeignKey(m => m.GameSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.QLServer)
                .WithMany(s => s.Matches)
                .HasForeignKey(m => m.QLServerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchPlayer>(e =>
        {
            e.HasIndex(mp => new { mp.MatchId, mp.PlayerId }).IsUnique();
            e.Property(mp => mp.Team).HasMaxLength(8);
            e.HasOne(mp => mp.Match)
                .WithMany(m => m.MatchPlayers)
                .HasForeignKey(mp => mp.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(mp => mp.Player)
                .WithMany(p => p.MatchPlayers)
                .HasForeignKey(mp => mp.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RoundResult>(e =>
        {
            e.HasIndex(r => new { r.MatchId, r.RoundNumber });
            e.Property(r => r.TeamWon).HasMaxLength(8);
            e.HasOne(r => r.Match)
                .WithMany(m => m.RoundResults)
                .HasForeignKey(r => r.MatchId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ZmqEvent>(e =>
        {
            e.HasIndex(z => z.Processed);
            e.HasIndex(z => z.ReceivedAt);
            e.Property(z => z.EventType).HasMaxLength(64);
            e.HasOne(z => z.QLServer)
                .WithMany(s => s.ZmqEvents)
                .HasForeignKey(z => z.QLServerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ScoringRule>(e =>
        {
            e.Property(r => r.Value).HasPrecision(10, 6);
            e.Property(r => r.Threshold).HasPrecision(10, 6);
            e.Property(r => r.GameTypeFilter).HasMaxLength(32);
            e.HasOne(r => r.Season)
                .WithMany(s => s.Rules)
                .HasForeignKey(r => r.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
