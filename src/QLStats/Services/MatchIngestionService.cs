using Microsoft.EntityFrameworkCore;
using QLStats.Data;
using QLStats.Data.Entities;

namespace QLStats.Services;

public class MatchIngestionService(
    IDbContextFactory<AppDbContext> dbFactory,
    LiveMatchState liveMatch,
    ILogger<MatchIngestionService> logger)
{
    public async Task HandleMatchStartedAsync(MatchStartedData data, int serverId, string serverName)
    {
        logger.LogInformation("Match started: {MatchGuid} on {Map}", data.MatchGuid, data.Map);

        await using var db = await dbFactory.CreateDbContextAsync();

        // Upsert today's session
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var session = await db.GameSessions.FirstOrDefaultAsync(s => s.SessionDate == today);
        if (session is null)
        {
            session = new GameSession { SessionDate = today };
            db.GameSessions.Add(session);
            await db.SaveChangesAsync();
        }

        // Create match idempotently
        var existing = await db.Matches.FirstOrDefaultAsync(m => m.MatchGuid == data.MatchGuid);
        if (existing is null)
        {
            db.Matches.Add(new Match
            {
                GameSessionId = session.Id,
                QLServerId = serverId,
                MatchGuid = data.MatchGuid,
                Map = data.Map,
                GameType = data.GameType,
                ServerTitle = data.ServerTitle,
                StartedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        liveMatch.StartMatch(data.MatchGuid, data.Map, data.GameType, serverName);
    }

    public async Task HandleMatchReportAsync(MatchReportData data, int serverId)
    {
        logger.LogInformation("Match report: {MatchGuid}, RED={Red} BLUE={Blue}, Aborted={Aborted}",
            data.MatchGuid, data.Tscore0, data.Tscore1, data.Aborted);

        await using var db = await dbFactory.CreateDbContextAsync();

        var match = await db.Matches.FirstOrDefaultAsync(m => m.MatchGuid == data.MatchGuid);
        if (match is null)
        {
            logger.LogWarning("Match {MatchGuid} not found for report; creating it", data.MatchGuid);
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var session = await db.GameSessions.FirstOrDefaultAsync(s => s.SessionDate == today);
            if (session is null)
            {
                session = new GameSession { SessionDate = today };
                db.GameSessions.Add(session);
                await db.SaveChangesAsync();
            }
            match = new Match
            {
                GameSessionId = session.Id,
                QLServerId = serverId,
                MatchGuid = data.MatchGuid,
                Map = data.Map,
                GameType = data.GameType,
                ServerTitle = data.ServerTitle,
                StartedAt = DateTime.UtcNow
            };
            db.Matches.Add(match);
            await db.SaveChangesAsync();
        }

        match.FinishedAt = DateTime.UtcNow;
        match.TeamRedRounds = data.Tscore0;
        match.TeamBlueRounds = data.Tscore1;

        // Back-fill rounds on any MatchPlayers already created by PLAYER_STATS events
        var existingPlayers = await db.MatchPlayers
            .Where(mp => mp.MatchId == match.Id)
            .ToListAsync();

        foreach (var mp in existingPlayers)
        {
            mp.RoundsWon = mp.Team == "RED" ? data.Tscore0 : data.Tscore1;
            mp.RoundsLost = mp.Team == "RED" ? data.Tscore1 : data.Tscore0;
        }

        await db.SaveChangesAsync();
        liveMatch.EndMatch();
    }

    public async Task HandlePlayerStatsAsync(PlayerStatsData data)
    {
        if (data.Warmup) return;

        await using var db = await dbFactory.CreateDbContextAsync();

        var match = await db.Matches.FirstOrDefaultAsync(m => m.MatchGuid == data.MatchGuid);
        if (match is null)
        {
            logger.LogWarning("Match {MatchGuid} not found for PLAYER_STATS (player {SteamId})",
                data.MatchGuid, data.SteamId);
            return;
        }

        var player = await EnsurePlayerAsync(db, data.SteamId, data.Name);
        var teamStr = data.Team == 1 ? "RED" : data.Team == 2 ? "BLUE" : "FREE";

        var existing = await db.MatchPlayers
            .FirstOrDefaultAsync(mp => mp.MatchId == match.Id && mp.PlayerId == player.Id);

        if (existing is null)
        {
            var mp = new MatchPlayer
            {
                MatchId = match.Id,
                PlayerId = player.Id,
                Team = teamStr,
                Won = data.Won,
                Kills = data.Kills,
                Deaths = data.Deaths,
                DamageDealt = data.DamageDealt,
                DamageTaken = data.DamageTaken
            };

            // Populate rounds if MATCH_REPORT already arrived
            if (match.TeamRedRounds.HasValue && match.TeamBlueRounds.HasValue)
            {
                mp.RoundsWon = teamStr == "RED" ? match.TeamRedRounds.Value : match.TeamBlueRounds.Value;
                mp.RoundsLost = teamStr == "RED" ? match.TeamBlueRounds.Value : match.TeamRedRounds.Value;
            }

            db.MatchPlayers.Add(mp);
        }
        else
        {
            existing.Team = teamStr;
            existing.Won = data.Won;
            existing.Kills = data.Kills;
            existing.Deaths = data.Deaths;
            existing.DamageDealt = data.DamageDealt;
            existing.DamageTaken = data.DamageTaken;

            if (match.TeamRedRounds.HasValue && match.TeamBlueRounds.HasValue)
            {
                existing.RoundsWon = teamStr == "RED" ? match.TeamRedRounds.Value : match.TeamBlueRounds.Value;
                existing.RoundsLost = teamStr == "RED" ? match.TeamBlueRounds.Value : match.TeamRedRounds.Value;
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task HandlePlayerKillAsync(PlayerKillData data)
    {
        liveMatch.RecordKill(data.KillerSteamId, data.KillerName, data.VictimSteamId, data.VictimName, data.Mod);
        await Task.CompletedTask;
    }

    public async Task HandleRoundOverAsync(RoundOverData data)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var match = await db.Matches.FirstOrDefaultAsync(m => m.MatchGuid == data.MatchGuid);
        if (match is not null)
        {
            var existing = await db.RoundResults
                .FirstOrDefaultAsync(r => r.MatchId == match.Id && r.RoundNumber == data.RoundNumber);
            if (existing is null)
            {
                db.RoundResults.Add(new RoundResult
                {
                    MatchId = match.Id,
                    RoundNumber = data.RoundNumber,
                    TeamWon = data.WinningTeam
                });
                await db.SaveChangesAsync();
            }
        }
        liveMatch.RecordRoundOver(data.WinningTeam == "RED" ? 1 : 2, data.RoundNumber);
    }

    public async Task HandlePlayerConnectAsync(PlayerConnectData data)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await EnsurePlayerAsync(db, data.SteamId, data.Name);
        await db.SaveChangesAsync();
    }

    private static async Task<Player> EnsurePlayerAsync(AppDbContext db, string steamId, string name)
    {
        var player = await db.Players.FirstOrDefaultAsync(p => p.SteamId == steamId);
        if (player is null)
        {
            player = new Player
            {
                SteamId = steamId,
                DisplayName = name,
                FirstSeenAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow
            };
            db.Players.Add(player);
            await db.SaveChangesAsync();
        }
        else
        {
            player.LastSeenAt = DateTime.UtcNow;
            if (player.DisplayName == player.SteamId || string.IsNullOrWhiteSpace(player.DisplayName))
                player.DisplayName = name;
        }
        return player;
    }
}
