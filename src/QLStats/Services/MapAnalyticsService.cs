using Microsoft.EntityFrameworkCore;
using QLStats.Data;

namespace QLStats.Services;

public record MapSummary(
    string MapName,
    int TotalMatches,
    int RedWins,
    int BlueWins,
    DateTime? LastPlayed,
    List<MapTopPlayer> Top3Players
);

public record MapTopPlayer(
    int PlayerId,
    string DisplayName,
    int Matches,
    int Wins,
    double WinRate
);

public record MapPlayerStats(
    int PlayerId,
    string DisplayName,
    int Matches,
    int Wins,
    int Losses,
    double WinRate,
    int Kills,
    int Deaths,
    double KD,
    int DamageDealt,
    Dictionary<string, int> TopMedals
);

public record MapGameTypeBreakdown(
    string GameType,
    int Count
);

public class MapAnalyticsService(AppDbContext db)
{
    public async Task<List<MapSummary>> GetMapSummariesAsync()
    {
        var matches = await db.Matches
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .Where(m => m.FinishedAt != null)
            .ToListAsync();

        return matches
            .GroupBy(m => m.Map)
            .Select(g =>
            {
                var mapMatches = g.ToList();
                var redWins = mapMatches.Count(m => m.TeamRedRounds > m.TeamBlueRounds);
                var blueWins = mapMatches.Count(m => m.TeamBlueRounds > m.TeamRedRounds);
                var lastPlayed = mapMatches.Max(m => m.StartedAt);

                var playerStats = mapMatches
                    .SelectMany(m => m.MatchPlayers)
                    .GroupBy(mp => new { mp.PlayerId, mp.Player.DisplayName })
                    .Select(pg => new MapTopPlayer(
                        PlayerId: pg.Key.PlayerId,
                        DisplayName: pg.Key.DisplayName,
                        Matches: pg.Count(),
                        Wins: pg.Count(mp => mp.Won),
                        WinRate: pg.Count() == 0 ? 0 : Math.Round((double)pg.Count(mp => mp.Won) / pg.Count() * 100, 1)
                    ))
                    .OrderByDescending(p => p.WinRate)
                    .ThenByDescending(p => p.Matches)
                    .Take(3)
                    .ToList();

                return new MapSummary(
                    MapName: g.Key,
                    TotalMatches: mapMatches.Count,
                    RedWins: redWins,
                    BlueWins: blueWins,
                    LastPlayed: lastPlayed,
                    Top3Players: playerStats
                );
            })
            .OrderByDescending(m => m.TotalMatches)
            .ToList();
    }

    public async Task<List<MapPlayerStats>> GetMapPlayerStatsAsync(string mapName)
    {
        var matchPlayers = await db.MatchPlayers
            .Include(mp => mp.Player)
            .Include(mp => mp.Match)
            .Where(mp => mp.Match.Map == mapName && mp.Match.FinishedAt != null)
            .ToListAsync();

        return matchPlayers
            .GroupBy(mp => new { mp.PlayerId, mp.Player.DisplayName })
            .Select(g =>
            {
                var kills = g.Sum(mp => mp.Kills);
                var deaths = g.Sum(mp => mp.Deaths);
                var wins = g.Count(mp => mp.Won);
                var matches = g.Count();

                var allMedals = g.SelectMany(mp => mp.Medals)
                    .GroupBy(kv => kv.Key)
                    .ToDictionary(mg => mg.Key, mg => mg.Sum(kv => kv.Value));
                var topMedals = allMedals
                    .OrderByDescending(kv => kv.Value)
                    .Take(3)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);

                return new MapPlayerStats(
                    PlayerId: g.Key.PlayerId,
                    DisplayName: g.Key.DisplayName,
                    Matches: matches,
                    Wins: wins,
                    Losses: matches - wins,
                    WinRate: matches == 0 ? 0 : Math.Round((double)wins / matches * 100, 1),
                    Kills: kills,
                    Deaths: deaths,
                    KD: deaths == 0 ? kills : Math.Round((double)kills / deaths, 2),
                    DamageDealt: g.Sum(mp => mp.DamageDealt),
                    TopMedals: topMedals
                );
            })
            .OrderByDescending(p => p.WinRate)
            .ThenByDescending(p => p.Matches)
            .ToList();
    }

    public async Task<List<MapGameTypeBreakdown>> GetMapGameTypeBreakdownAsync(string mapName)
    {
        var breakdowns = await db.Matches
            .Where(m => m.Map == mapName && m.FinishedAt != null)
            .GroupBy(m => m.GameType)
            .Select(g => new MapGameTypeBreakdown(g.Key, g.Count()))
            .OrderByDescending(b => b.Count)
            .ToListAsync();

        return breakdowns;
    }
}
