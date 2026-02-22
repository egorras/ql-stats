using Microsoft.EntityFrameworkCore;
using QLStats.Data;
using QLStats.Data.Entities;
using QLStats.Events;

namespace QLStats.Services;

public class MatchIngestionService(
    IDbContextFactory<AppDbContext> dbFactory,
    LiveMatchState liveMatch,
    ILogger<MatchIngestionService> logger)
{
    public Task HandleAsync(QlEvent @event, int serverId) => @event switch
    {
        MatchStartedData e  => HandleMatchStartedAsync(e, serverId),
        MatchReportData e   => HandleMatchReportAsync(e, serverId),
        PlayerKillData e    => HandlePlayerKillAsync(e),
        RoundOverData e     => HandleRoundOverAsync(e),
        PlayerStatsData e   => HandlePlayerStatsAsync(e),
        PlayerConnectData e => HandlePlayerConnectAsync(e),
        PlayerMedalData e   => HandlePlayerMedalAsync(e),
        _                   => Task.CompletedTask
    };

    private async Task HandleMatchStartedAsync(MatchStartedData data, int serverId)
    {
        logger.LogInformation("Match started: {MatchGuid} on {Map}", data.MatchGuid, data.Map);

        await using var db = await dbFactory.CreateDbContextAsync();

        var session = await EnsureSessionAsync(db);
        var matchGuid = data.MatchGuid.ToString();

        if (!await db.Matches.AnyAsync(m => m.MatchGuid == matchGuid))
        {
            var match = new Match { GameSessionId = session.Id, QLServerId = serverId };
            match.Apply(data);
            db.Matches.Add(match);
            await db.SaveChangesAsync();
        }

        var livePlayers = data.Players?.Select(p =>
            (p.SteamId, p.Name, p.Team == 1 ? "RED" : p.Team == 2 ? "BLUE" : ""));
        liveMatch.StartMatch(matchGuid, data.Map, data.GameType, data.ServerTitle, livePlayers);
    }

    private async Task HandleMatchReportAsync(MatchReportData data, int serverId)
    {
        logger.LogInformation("Match report: {MatchGuid}, RED={Red} BLUE={Blue}, Aborted={Aborted}",
            data.MatchGuid, data.Tscore0, data.Tscore1, data.Aborted);

        await using var db = await dbFactory.CreateDbContextAsync();

        var matchGuid = data.MatchGuid.ToString();
        var match = await db.Matches.FirstOrDefaultAsync(m => m.MatchGuid == matchGuid);
        if (match is null)
        {
            logger.LogWarning("Match {MatchGuid} not found for report; creating it", matchGuid);
            var session = await EnsureSessionAsync(db);
            match = new Match { GameSessionId = session.Id, QLServerId = serverId };
            db.Matches.Add(match);
        }

        match.Apply(data);

        // Back-fill rounds on any MatchPlayers already created by PLAYER_STATS events
        var players = await db.MatchPlayers.Where(mp => mp.MatchId == match.Id).ToListAsync();
        foreach (var mp in players)
        {
            mp.RoundsWon = mp.Team == "RED" ? data.Tscore0 : data.Tscore1;
            mp.RoundsLost = mp.Team == "RED" ? data.Tscore1 : data.Tscore0;
        }

        await db.SaveChangesAsync();

        if (liveMatch.Current?.MatchGuid == matchGuid || liveMatch.Current?.Map == "")
            liveMatch.UpdateMatchInfo(data.Map, data.GameType, data.ServerTitle);

        liveMatch.EndMatch();
    }

    private async Task HandlePlayerStatsAsync(PlayerStatsData data)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var matchGuid = data.MatchGuid.ToString();
        var match = await db.Matches.FirstOrDefaultAsync(m => m.MatchGuid == matchGuid);
        if (match is null)
        {
            logger.LogWarning("Match {MatchGuid} not found for PLAYER_STATS (player {SteamId})",
                matchGuid, data.SteamId);
            return;
        }

        var player = await EnsurePlayerAsync(db, data.SteamId, data.Name);
        var mp = await db.MatchPlayers
            .FirstOrDefaultAsync(x => x.MatchId == match.Id && x.PlayerId == player.Id);

        if (mp is null)
        {
            mp = new MatchPlayer { MatchId = match.Id, PlayerId = player.Id };
            db.MatchPlayers.Add(mp);
        }

        mp.Apply(data);

        // Populate rounds if MATCH_REPORT already arrived
        if (match.TeamRedRounds.HasValue && match.TeamBlueRounds.HasValue)
        {
            mp.RoundsWon = mp.Team == "RED" ? match.TeamRedRounds.Value : match.TeamBlueRounds.Value;
            mp.RoundsLost = mp.Team == "RED" ? match.TeamBlueRounds.Value : match.TeamRedRounds.Value;
        }

        await db.SaveChangesAsync();
    }

    private async Task HandlePlayerKillAsync(PlayerKillData data)
    {
        if (liveMatch.Current is null && data.MatchGuid != Guid.Empty)
            liveMatch.StartMatch(data.MatchGuid.ToString(), "", "", "");

        liveMatch.RecordKill(
            data.Killer?.SteamId ?? "", data.Killer?.Name ?? "",
            data.Victim?.SteamId ?? "", data.Victim?.Name ?? "",
            data.Mod);

        await Task.CompletedTask;
    }

    private async Task HandleRoundOverAsync(RoundOverData data)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var matchGuid = data.MatchGuid.ToString();
        var match = await db.Matches.FirstOrDefaultAsync(m => m.MatchGuid == matchGuid);
        if (match is not null)
        {
            var exists = await db.RoundResults
                .AnyAsync(r => r.MatchId == match.Id && r.RoundNumber == data.Round);
            if (!exists)
            {
                db.RoundResults.Add(new RoundResult
                {
                    MatchId = match.Id,
                    RoundNumber = data.Round,
                    TeamWon = data.TeamWon
                });
                await db.SaveChangesAsync();
            }
        }
        liveMatch.RecordRoundOver(data.TeamWon == "RED" ? 1 : 2, data.Round);
    }

    private async Task HandlePlayerMedalAsync(PlayerMedalData data)
    {
        liveMatch.RecordMedal(data.SteamId, data.Name, data.Medal, data.Total);

        await using var db = await dbFactory.CreateDbContextAsync();
        var matchGuid = data.MatchGuid.ToString();
        var match = await db.Matches.FirstOrDefaultAsync(m => m.MatchGuid == matchGuid);
        if (match is null) return;

        var player = await EnsurePlayerAsync(db, data.SteamId, data.Name);
        var mp = await db.MatchPlayers
            .FirstOrDefaultAsync(x => x.MatchId == match.Id && x.PlayerId == player.Id);

        if (mp is null)
        {
            mp = new MatchPlayer { MatchId = match.Id, PlayerId = player.Id };
            db.MatchPlayers.Add(mp);
        }

        mp.Medals[data.Medal] = data.Total;
        await db.SaveChangesAsync();
    }

    private async Task HandlePlayerConnectAsync(PlayerConnectData data)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await EnsurePlayerAsync(db, data.SteamId, data.Name);
        await db.SaveChangesAsync();
    }

    private static async Task<GameSession> EnsureSessionAsync(AppDbContext db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var session = await db.GameSessions.FirstOrDefaultAsync(s => s.SessionDate == today);
        if (session is not null) return session;

        session = new GameSession { SessionDate = today };
        db.GameSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
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
