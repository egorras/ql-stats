using Microsoft.EntityFrameworkCore;
using QLStats.Data;

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
    int Wins,
    int Losses,
    int RoundsWon,
    int RoundsLost,
    int DamageDealt,
    int MatchesPlayed
);

public class StandingsService(AppDbContext db)
{
    public async Task<List<PlayerStanding>> GetSeasonStandingsAsync(int seasonId)
    {
        var season = await db.Seasons
            .Include(s => s.Rules)
            .FirstOrDefaultAsync(s => s.Id == seasonId)
            ?? throw new InvalidOperationException($"Season {seasonId} not found");

        var startDt = season.StartDate.ToDateTime(TimeOnly.MinValue);
        var endDt = season.EndDate.HasValue
            ? season.EndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)
            : DateTime.MaxValue;

        // Load all match players whose match falls within the season's date range
        var matchPlayers = await db.MatchPlayers
            .Include(mp => mp.Player)
            .Include(mp => mp.Match)
            .Where(mp => mp.Match.StartedAt >= startDt && mp.Match.StartedAt < endDt
                         && mp.Match.FinishedAt != null)
            .ToListAsync();

        var grouped = matchPlayers
            .GroupBy(mp => new { mp.PlayerId, mp.Player.DisplayName });

        var standings = grouped.Select(g =>
        {
            var kills = g.Sum(mp => mp.Kills);
            var deaths = g.Sum(mp => mp.Deaths);
            var wins = g.Count(mp => mp.Won);
            var losses = g.Count(mp => !mp.Won);
            var roundsWon = g.Sum(mp => mp.RoundsWon);
            var roundsLost = g.Sum(mp => mp.RoundsLost);
            var damage = g.Sum(mp => mp.DamageDealt);

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
                Wins: wins,
                Losses: losses,
                RoundsWon: roundsWon,
                RoundsLost: roundsLost,
                DamageDealt: damage,
                MatchesPlayed: g.Select(mp => mp.MatchId).Distinct().Count()
            );
        })
        .OrderByDescending(s => s.TotalPoints)
        .ToList();

        return standings;
    }
}
