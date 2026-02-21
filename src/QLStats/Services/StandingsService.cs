using Microsoft.EntityFrameworkCore;
using QLStats.Data;

namespace QLStats.Services;

public record PlayerStanding(
    int PlayerId,
    string DisplayName,
    decimal TotalPoints,
    // breakdown
    decimal PointsFromKills,
    decimal PointsFromDeaths,
    decimal PointsFromWins,
    decimal PointsFromLosses,
    decimal PointsFromRoundsWon,
    decimal PointsFromRoundsLost,
    decimal PointsFromDamage,
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
        var season = await db.Seasons.FindAsync(seasonId)
            ?? throw new InvalidOperationException($"Season {seasonId} not found");

        // Load all match players for sessions in this season
        var matchPlayers = await db.MatchPlayers
            .Include(mp => mp.Player)
            .Include(mp => mp.Match)
                .ThenInclude(m => m.GameSession)
            .Where(mp => mp.Match.GameSession.SeasonId == seasonId && mp.Match.FinishedAt != null)
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

            var fromKills = kills * season.PointsPerKill;
            var fromDeaths = deaths * season.PointsPerDeath;
            var fromWins = wins * season.PointsPerWin;
            var fromLosses = losses * season.PointsPerLoss;
            var fromRoundsWon = roundsWon * season.PointsPerRoundWon;
            var fromRoundsLost = roundsLost * season.PointsPerRoundLost;
            var fromDamage = damage * season.PointsPerDamageDealt;

            return new PlayerStanding(
                PlayerId: g.Key.PlayerId,
                DisplayName: g.Key.DisplayName,
                TotalPoints: fromKills + fromDeaths + fromWins + fromLosses + fromRoundsWon + fromRoundsLost + fromDamage,
                PointsFromKills: fromKills,
                PointsFromDeaths: fromDeaths,
                PointsFromWins: fromWins,
                PointsFromLosses: fromLosses,
                PointsFromRoundsWon: fromRoundsWon,
                PointsFromRoundsLost: fromRoundsLost,
                PointsFromDamage: fromDamage,
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
