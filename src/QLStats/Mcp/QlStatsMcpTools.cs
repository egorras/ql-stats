using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using QLStats.Data;
using QLStats.Services;
using System.ComponentModel;

namespace QLStats.Mcp;

[McpServerToolType]
public class QlStatsMcpTools(AppDbContext db, DuoAnalyticsService duoService, MapAnalyticsService mapService)
{
    [McpServerTool, Description("List all seasons with their Id, Name, IsActive, StartDate and EndDate.")]
    public async Task<object> ListSeasons()
    {
        var seasons = await db.Seasons
            .OrderByDescending(s => s.IsActive)
            .ThenByDescending(s => s.StartDate)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.IsActive,
                StartDate = s.StartDate.ToString("yyyy-MM-dd"),
                EndDate = s.EndDate.HasValue ? s.EndDate.Value.ToString("yyyy-MM-dd") : null
            })
            .ToListAsync();
        return seasons;
    }

    [McpServerTool, Description("Get the full leaderboard for a season from the persisted standings snapshot. Includes points, raw stats and K/D. SnappedAt shows when standings were last computed.")]
    public async Task<object> GetSeasonStandings([Description("The season Id")] int seasonId)
    {
        var rows = await db.SeasonStandings
            .Include(ss => ss.Player)
            .Where(ss => ss.SeasonId == seasonId)
            .OrderByDescending(ss => ss.TotalPoints)
            .ToListAsync();

        if (rows.Count == 0)
            return new { SeasonId = seasonId, Message = "No standings snapshot yet — run a Full Rebuild or wait for the next match to finish." };

        return rows.Select(s => new
        {
            s.PlayerId,
            DisplayName = s.Player.DisplayName,
            TotalPoints = Math.Round(s.TotalPoints, 2),
            s.Kills,
            s.Deaths,
            KD = s.Deaths == 0 ? (double)s.Kills : Math.Round((double)s.Kills / s.Deaths, 2),
            s.Wins,
            s.Losses,
            s.RoundsWon,
            s.RoundsLost,
            s.DamageDealt,
            s.MatchesPlayed,
            SnappedAt = s.SnappedAt.ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();
    }

    [McpServerTool, Description("List all registered players with their Id, DisplayName, SteamId and last seen date.")]
    public async Task<object> ListPlayers()
    {
        var players = await db.Players
            .OrderBy(p => p.DisplayName)
            .Select(p => new
            {
                p.Id,
                p.DisplayName,
                p.SteamId,
                FirstSeen = p.FirstSeenAt.ToString("yyyy-MM-dd"),
                LastSeen = p.LastSeenAt.ToString("yyyy-MM-dd")
            })
            .ToListAsync();
        return players;
    }

    [McpServerTool, Description("Get detailed stats for a player across all matches, optionally filtered by seasonId.")]
    public async Task<object> GetPlayerStats(
        [Description("The player Id")] int playerId,
        [Description("Optional season Id to filter by")] int? seasonId = null)
    {
        var query = db.MatchPlayers
            .Include(mp => mp.Match)
            .Include(mp => mp.Player)
            .Where(mp => mp.PlayerId == playerId && mp.Match.FinishedAt != null && !mp.Match.IsArchived);

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

        var rows = await query.ToListAsync();
        if (rows.Count == 0)
            return new { PlayerId = playerId, Message = "No matches found" };

        var player = rows[0].Player;
        return new
        {
            player.Id,
            player.DisplayName,
            player.SteamId,
            MatchesPlayed = rows.Select(r => r.MatchId).Distinct().Count(),
            Wins = rows.Count(r => r.Won),
            Losses = rows.Count(r => !r.Won),
            Kills = rows.Sum(r => r.Kills),
            Deaths = rows.Sum(r => r.Deaths),
            KD = rows.Sum(r => r.Deaths) == 0 ? (double)rows.Sum(r => r.Kills)
                : Math.Round((double)rows.Sum(r => r.Kills) / rows.Sum(r => r.Deaths), 2),
            DamageDealt = rows.Sum(r => r.DamageDealt),
            RoundsWon = rows.Sum(r => r.RoundsWon),
            RoundsLost = rows.Sum(r => r.RoundsLost),
            RecentMatches = rows
                .OrderByDescending(r => r.Match.StartedAt)
                .Take(5)
                .Select(r => new
                {
                    r.MatchId,
                    r.Match.Map,
                    StartedAt = r.Match.StartedAt.ToString("yyyy-MM-dd HH:mm"),
                    r.Team,
                    r.Won,
                    r.Kills,
                    r.Deaths,
                    r.DamageDealt
                }).ToList()
        };
    }

    [McpServerTool, Description("Get full match breakdown: map, teams, per-player stats and round results.")]
    public async Task<object> GetMatchDetails([Description("The match Id")] int matchId)
    {
        var match = await db.Matches
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .Include(m => m.RoundResults)
            .Include(m => m.QLServer)
            .FirstOrDefaultAsync(m => m.Id == matchId);

        if (match is null) return new { Error = $"Match {matchId} not found" };

        return new
        {
            match.Id,
            match.Map,
            match.GameType,
            match.ServerTitle,
            Server = match.QLServer?.ZmqAddress,
            StartedAt = match.StartedAt.ToString("yyyy-MM-dd HH:mm"),
            FinishedAt = match.FinishedAt?.ToString("yyyy-MM-dd HH:mm"),
            match.TeamRedRounds,
            match.TeamBlueRounds,
            Players = match.MatchPlayers.Select(mp => new
            {
                mp.Player.DisplayName,
                mp.Team,
                mp.Won,
                mp.Kills,
                mp.Deaths,
                KD = mp.Deaths == 0 ? (double)mp.Kills : Math.Round((double)mp.Kills / mp.Deaths, 2),
                mp.DamageDealt,
                mp.RoundsWon,
                mp.RoundsLost,
                Weapons = mp.Weapons.OrderByDescending(w => w.Value)
                    .ToDictionary(w => w.Key, w => w.Value)
            }).OrderBy(p => p.Team).ToList(),
            Rounds = match.RoundResults.OrderBy(r => r.RoundNumber).Select(r => new
            {
                r.RoundNumber,
                r.TeamWon
            }).ToList()
        };
    }

    [McpServerTool, Description("Query matches with optional filters: playerId, map name substring, date range (ISO 8601), seasonId. Returns most recent first.")]
    public async Task<object> QueryMatches(
        [Description("Filter by player Id (optional)")] int? playerId = null,
        [Description("Filter by map name substring (optional)")] string? map = null,
        [Description("Filter matches on or after this date: yyyy-MM-dd (optional)")] string? fromDate = null,
        [Description("Filter matches on or before this date: yyyy-MM-dd (optional)")] string? toDate = null,
        [Description("Filter by season Id (optional)")] int? seasonId = null,
        [Description("Max results to return (default 20)")] int limit = 20)
    {
        var query = db.Matches
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .Where(m => m.FinishedAt != null && !m.IsArchived)
            .AsQueryable();

        if (playerId.HasValue)
            query = query.Where(m => m.MatchPlayers.Any(mp => mp.PlayerId == playerId));

        if (!string.IsNullOrWhiteSpace(map))
            query = query.Where(m => m.Map.Contains(map));

        if (!string.IsNullOrWhiteSpace(fromDate) && DateOnly.TryParse(fromDate, out var from))
            query = query.Where(m => DateOnly.FromDateTime(m.StartedAt) >= from);

        if (!string.IsNullOrWhiteSpace(toDate) && DateOnly.TryParse(toDate, out var to))
            query = query.Where(m => DateOnly.FromDateTime(m.StartedAt) <= to);

        if (seasonId.HasValue)
        {
            var season = await db.Seasons.FindAsync(seasonId.Value);
            if (season is not null)
            {
                var startDt = DateTime.SpecifyKind(season.StartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
                var endDt = season.EndDate.HasValue
                    ? DateTime.SpecifyKind(season.EndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc)
                    : DateTime.MaxValue;
                query = query.Where(m => m.StartedAt >= startDt && m.StartedAt < endDt);
            }
        }

        var matches = await query
            .OrderByDescending(m => m.StartedAt)
            .Take(limit)
            .ToListAsync();

        return matches.Select(m => new
        {
            m.Id,
            m.Map,
            m.GameType,
            StartedAt = m.StartedAt.ToString("yyyy-MM-dd HH:mm"),
            m.TeamRedRounds,
            m.TeamBlueRounds,
            Players = m.MatchPlayers.Select(mp => new
            {
                mp.Player.DisplayName,
                mp.Team,
                mp.Won,
                mp.Kills,
                mp.Deaths
            }).ToList()
        }).ToList();
    }

    [McpServerTool, Description("Get top duo pairs by win rate. Players who played together on the same team. Optional filters: seasonId, mapName, limit.")]
    public async Task<object> GetTopDuos(
        [Description("Filter by season Id (optional)")] int? seasonId = null,
        [Description("Filter by exact map name (optional)")] string? mapName = null,
        [Description("Max results to return (default 20)")] int limit = 20)
    {
        var duos = await duoService.GetDuoStatsAsync(seasonId, mapName, limit);
        return duos.Select(d => new
        {
            d.Player1Id,
            d.Player1Name,
            d.Player2Id,
            d.Player2Name,
            d.MatchesPlayed,
            d.Wins,
            d.Losses,
            d.WinRate,
            d.CombinedKills,
            d.CombinedDamage,
            d.AvgKillsPerMatch,
            d.AvgDamagePerMatch
        }).ToList();
    }

    [McpServerTool, Description("Get analytics for a specific map: per-player stats, game type breakdown, and top duo pairs.")]
    public async Task<object> GetMapStats([Description("Exact map name")] string mapName)
    {
        var playerStats = await mapService.GetMapPlayerStatsAsync(mapName);
        var gameTypes = await mapService.GetMapGameTypeBreakdownAsync(mapName);
        var duos = await duoService.GetDuoStatsAsync(mapName: mapName, topN: 10);

        if (playerStats.Count == 0)
            return new { Error = $"No data found for map '{mapName}'" };

        return new
        {
            MapName = mapName,
            GameTypes = gameTypes.Select(g => new { g.GameType, g.Count }).ToList(),
            Players = playerStats.Select(p => new
            {
                p.PlayerId,
                p.DisplayName,
                p.Matches,
                p.Wins,
                p.Losses,
                p.WinRate,
                p.Kills,
                p.Deaths,
                p.KD,
                p.DamageDealt
            }).ToList(),
            TopDuos = duos.Select(d => new
            {
                d.Player1Name,
                d.Player2Name,
                d.MatchesPlayed,
                d.Wins,
                d.WinRate
            }).ToList()
        };
    }

    [McpServerTool, Description("Get win/loss head-to-head record between two players when they are on opposing teams.")]
    public async Task<object> GetHeadToHead(
        [Description("First player Id")] int player1Id,
        [Description("Second player Id")] int player2Id)
    {
        var p1 = await db.Players.FindAsync(player1Id);
        var p2 = await db.Players.FindAsync(player2Id);
        if (p1 is null || p2 is null) return new { Error = "Player not found" };

        // Find all matches where both players participated on opposing teams
        var matchIds = await db.MatchPlayers
            .Where(mp => (mp.PlayerId == player1Id || mp.PlayerId == player2Id) && !mp.Match.IsArchived)
            .GroupBy(mp => mp.MatchId)
            .Where(g => g.Count() == 2 && g.Select(mp => mp.Team).Distinct().Count() == 2)
            .Select(g => g.Key)
            .ToListAsync();

        if (matchIds.Count == 0)
            return new { Player1 = p1.DisplayName, Player2 = p2.DisplayName, HeadToHeadMatches = 0 };

        var rows = await db.MatchPlayers
            .Where(mp => matchIds.Contains(mp.MatchId) && (mp.PlayerId == player1Id || mp.PlayerId == player2Id))
            .Include(mp => mp.Match)
            .ToListAsync();

        int p1Wins = 0, p1Losses = 0;
        foreach (var matchId in matchIds)
        {
            var r1 = rows.FirstOrDefault(r => r.MatchId == matchId && r.PlayerId == player1Id);
            if (r1 is not null)
            {
                if (r1.Won) p1Wins++;
                else p1Losses++;
            }
        }

        return new
        {
            Player1 = p1.DisplayName,
            Player2 = p2.DisplayName,
            HeadToHeadMatches = matchIds.Count,
            Player1Wins = p1Wins,
            Player2Wins = p1Losses,
            Player1WinRate = matchIds.Count == 0 ? 0 : Math.Round((double)p1Wins / matchIds.Count * 100, 1)
        };
    }
}
