using Microsoft.EntityFrameworkCore;
using QLStats.Data;

namespace QLStats.Services;

public record DuoStats(
    int Player1Id,
    string Player1Name,
    int Player2Id,
    string Player2Name,
    int MatchesPlayed,
    int Wins,
    int Losses,
    double WinRate,
    int CombinedKills,
    int CombinedDamage,
    double AvgKillsPerMatch,
    double AvgDamagePerMatch
);

public class DuoAnalyticsService(AppDbContext db)
{
    public async Task<List<DuoStats>> GetDuoStatsAsync(
        int? seasonId = null,
        string? mapName = null,
        int topN = 20)
    {
        var query = db.MatchPlayers
            .Include(mp => mp.Player)
            .Include(mp => mp.Match)
            .Where(mp => mp.Match.FinishedAt != null)
            .AsQueryable();

        if (seasonId.HasValue)
        {
            var season = await db.Seasons.FindAsync(seasonId.Value);
            if (season is not null)
            {
                var startDt = DateTime.SpecifyKind(season.StartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                var endDt = season.EndDate.HasValue
                    ? DateTime.SpecifyKind(season.EndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                    : DateTime.MaxValue;
                query = query.Where(mp => mp.Match.StartedAt >= startDt && mp.Match.StartedAt < endDt);
            }
        }

        if (!string.IsNullOrWhiteSpace(mapName))
            query = query.Where(mp => mp.Match.Map == mapName);

        var matchPlayers = await query.ToListAsync();

        // Group by match, then self-join on same team, Player1Id < Player2Id to avoid duplicates
        var pairs = matchPlayers
            .GroupBy(mp => mp.MatchId)
            .SelectMany(g =>
            {
                var list = g.ToList();
                return from mp1 in list
                       from mp2 in list
                       where mp1.PlayerId < mp2.PlayerId && mp1.Team == mp2.Team
                       select new
                       {
                           Player1 = mp1,
                           Player2 = mp2,
                           Won = mp1.Won
                       };
            })
            .GroupBy(p => (p.Player1.PlayerId, p.Player2.PlayerId))
            .Select(g =>
            {
                var first = g.First();
                var matches = g.Count();
                var wins = g.Count(x => x.Won);
                var combinedKills = g.Sum(x => x.Player1.Kills + x.Player2.Kills);
                var combinedDamage = g.Sum(x => x.Player1.DamageDealt + x.Player2.DamageDealt);

                return new DuoStats(
                    Player1Id: first.Player1.PlayerId,
                    Player1Name: first.Player1.Player.DisplayName,
                    Player2Id: first.Player2.PlayerId,
                    Player2Name: first.Player2.Player.DisplayName,
                    MatchesPlayed: matches,
                    Wins: wins,
                    Losses: matches - wins,
                    WinRate: matches == 0 ? 0 : Math.Round((double)wins / matches * 100, 1),
                    CombinedKills: combinedKills,
                    CombinedDamage: combinedDamage,
                    AvgKillsPerMatch: matches == 0 ? 0 : Math.Round((double)combinedKills / matches, 1),
                    AvgDamagePerMatch: matches == 0 ? 0 : Math.Round((double)combinedDamage / matches, 0)
                );
            })
            .OrderByDescending(d => d.WinRate)
            .ThenByDescending(d => d.MatchesPlayed)
            .Take(topN)
            .ToList();

        return pairs;
    }
}
