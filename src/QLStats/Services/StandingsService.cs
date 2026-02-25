using Microsoft.EntityFrameworkCore;
using QLStats.Data;
using QLStats.Data.Entities;

namespace QLStats.Services;

public record PlayerStanding(
    int PlayerId,
    string DisplayName,
    decimal TotalPoints,
    // breakdown
    Dictionary<string, decimal> RulesBreakdown,
    // raw stats
    int Kills,
    int Deaths,
    int Suicides,
    int Wins,
    int Losses,
    int RoundsWon,
    int RoundsLost,
    int DamageDealt,
    int TotalMedals,
    int MatchesPlayed
);

public class StandingsService(
    IDbContextFactory<AppDbContext> dbFactory,
    ILogger<StandingsService> logger)
{
    public async Task<List<PlayerStanding>> GetSeasonStandingsAsync(int seasonId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var season = await db.Seasons
            .Include(s => s.Rules)
            .FirstOrDefaultAsync(s => s.Id == seasonId)
            ?? throw new InvalidOperationException($"Season {seasonId} not found");

        var startDt = DateTime.SpecifyKind(season.StartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var endDt = season.EndDate.HasValue
            ? DateTime.SpecifyKind(season.EndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
            : DateTime.MaxValue;

        var matchPlayers = await db.MatchPlayers
            .Include(mp => mp.Player)
            .Include(mp => mp.Match)
            .Where(mp => mp.Match.StartedAt >= startDt && mp.Match.StartedAt < endDt
                         && mp.Match.FinishedAt != null && !mp.Match.IsArchived)
            .ToListAsync();

        var grouped = matchPlayers
            .GroupBy(mp => new { mp.PlayerId, mp.Player.DisplayName });

        var standings = grouped.Select(g =>
        {
            var kills = g.Sum(mp => mp.Kills);
            var deaths = g.Sum(mp => mp.Deaths);
            var suicides = g.Sum(mp => mp.Suicides);
            var wins = g.Count(mp => mp.Won);
            var losses = g.Count(mp => !mp.Won);
            var roundsWon = g.Sum(mp => mp.RoundsWon);
            var roundsLost = g.Sum(mp => mp.RoundsLost);
            var damage = g.Sum(mp => mp.DamageDealt);
            var totalMedals = g.Sum(mp => mp.Medals.Values.Sum());

            var ruleBreakdown = new Dictionary<string, decimal>();
            decimal totalPoints = 0;

            foreach (var mp in g)
            {
                var (points, bd) = ScoringEngine.CalculatePointsWithBreakdown(mp, season);
                totalPoints += points;
                foreach (var (key, value) in bd)
                {
                    if (ruleBreakdown.ContainsKey(key))
                        ruleBreakdown[key] += value;
                    else
                        ruleBreakdown[key] = value;
                }
            }

            return new PlayerStanding(
                PlayerId: g.Key.PlayerId,
                DisplayName: g.Key.DisplayName,
                TotalPoints: totalPoints,
                RulesBreakdown: ruleBreakdown,
                Kills: kills,
                Deaths: deaths,
                Suicides: suicides,
                Wins: wins,
                Losses: losses,
                RoundsWon: roundsWon,
                RoundsLost: roundsLost,
                DamageDealt: damage,
                TotalMedals: totalMedals,
                MatchesPlayed: g.Select(mp => mp.MatchId).Distinct().Count()
            );
        })
        .OrderByDescending(s => s.TotalPoints)
        .ToList();

        return standings;
    }

    public async Task SnapshotSeasonAsync(int seasonId)
    {
        var standings = await GetSeasonStandingsAsync(seasonId);

        await using var db = await dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;

        foreach (var s in standings)
        {
            var existing = await db.SeasonStandings
                .FirstOrDefaultAsync(ss => ss.SeasonId == seasonId && ss.PlayerId == s.PlayerId);

            if (existing is null)
            {
                db.SeasonStandings.Add(new SeasonStanding
                {
                    SeasonId = seasonId,
                    PlayerId = s.PlayerId,
                    TotalPoints = s.TotalPoints,
                    Kills = s.Kills,
                    Deaths = s.Deaths,
                    Wins = s.Wins,
                    Losses = s.Losses,
                    RoundsWon = s.RoundsWon,
                    RoundsLost = s.RoundsLost,
                    DamageDealt = s.DamageDealt,
                    MatchesPlayed = s.MatchesPlayed,
                    SnappedAt = now
                });
            }
            else
            {
                existing.TotalPoints = s.TotalPoints;
                existing.Kills = s.Kills;
                existing.Deaths = s.Deaths;
                existing.Wins = s.Wins;
                existing.Losses = s.Losses;
                existing.RoundsWon = s.RoundsWon;
                existing.RoundsLost = s.RoundsLost;
                existing.DamageDealt = s.DamageDealt;
                existing.MatchesPlayed = s.MatchesPlayed;
                existing.SnappedAt = now;
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("Snapshotted standings for season {SeasonId}: {Count} players", seasonId, standings.Count);
    }

    public async Task SnapshotAllSeasonsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var seasonIds = await db.Seasons.Select(s => s.Id).ToListAsync();
        foreach (var id in seasonIds)
            await SnapshotSeasonAsync(id);
    }

    public async Task SnapshotSeasonForMatchAsync(DateTime matchStartedAt)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var matchDate = DateOnly.FromDateTime(matchStartedAt);
        var season = await db.Seasons
            .FirstOrDefaultAsync(s => s.StartDate <= matchDate && (s.EndDate == null || s.EndDate >= matchDate));
        if (season is not null)
            await SnapshotSeasonAsync(season.Id);
    }
}
