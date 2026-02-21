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
        logger.LogInformation("Match report: {MatchGuid}, RED={Red} BLUE={Blue}", data.MatchGuid, data.Tscore0, data.Tscore1);

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

        bool redWon = data.Tscore0 > data.Tscore1;

        foreach (var pd in data.Players)
        {
            var player = await EnsurePlayerAsync(db, pd.SteamId, pd.Name);
            var teamStr = pd.Team == 1 ? "RED" : "BLUE";
            var teamRoundsWon = pd.Team == 1 ? data.Tscore0 : data.Tscore1;
            var teamRoundsLost = pd.Team == 1 ? data.Tscore1 : data.Tscore0;
            var won = pd.Team == 1 ? redWon : !redWon;

            var existing = await db.MatchPlayers.FirstOrDefaultAsync(mp => mp.MatchId == match.Id && mp.PlayerId == player.Id);
            if (existing is null)
            {
                db.MatchPlayers.Add(new MatchPlayer
                {
                    MatchId = match.Id,
                    PlayerId = player.Id,
                    Team = teamStr,
                    Won = won,
                    Kills = pd.Kills,
                    Deaths = pd.Deaths,
                    DamageDealt = pd.DamageDealt,
                    RoundsWon = teamRoundsWon,
                    RoundsLost = teamRoundsLost
                });
            }
            else
            {
                existing.Team = teamStr;
                existing.Won = won;
                existing.Kills = pd.Kills;
                existing.Deaths = pd.Deaths;
                existing.DamageDealt = pd.DamageDealt;
                existing.RoundsWon = teamRoundsWon;
                existing.RoundsLost = teamRoundsLost;
            }
        }

        await db.SaveChangesAsync();
        liveMatch.EndMatch();
    }

    public async Task HandlePlayerKillAsync(PlayerKillData data)
    {
        liveMatch.RecordKill(data.KillerSteamId, data.KillerName, data.VictimSteamId, data.VictimName, data.Weapon);
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
                    TeamWon = data.WinningTeam == 1 ? "RED" : "BLUE"
                });
                await db.SaveChangesAsync();
            }
        }
        liveMatch.RecordRoundOver(data.WinningTeam, data.RoundNumber);
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
