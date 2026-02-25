using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using QLStats.Data;
using QLStats.Data.Entities;
using QLStats.Events;

namespace QLStats.Services;

public class MatchIngestionService(
    IDbContextFactory<AppDbContext> dbFactory,
    LiveMatchState liveMatch,
    StandingsNotifier standingsNotifier,
    StandingsService standingsService,
    ILogger<MatchIngestionService> logger)
{
    private const int InterRoundWindowSeconds = 11;
    private readonly ConcurrentDictionary<string, int> _roundEndTimes = new();

    public Task HandleAsync(QlEvent @event, int serverId, DateTime? eventTime = null, bool isReplay = false) => @event switch
    {
        MatchStartedData e  => HandleMatchStartedAsync(e, serverId, eventTime),
        MatchReportData e   => HandleMatchReportAsync(e, serverId, eventTime, isReplay),
        PlayerKillData e    => HandlePlayerKillAsync(e),
        RoundOverData e     => HandleRoundOverAsync(e),
        PlayerStatsData e   => HandlePlayerStatsAsync(e),
        PlayerConnectData e => HandlePlayerConnectAsync(e),
        PlayerMedalData e   => HandlePlayerMedalAsync(e),
        _                   => Task.CompletedTask
    };

    private async Task HandleMatchStartedAsync(MatchStartedData data, int serverId, DateTime? eventTime)
    {
        logger.LogInformation("Match started: {MatchGuid} on {Map}", data.MatchGuid, data.Map);

        await using var db = await dbFactory.CreateDbContextAsync();

        var matchGuid = data.MatchGuid.ToString();

        if (!await db.Matches.AnyAsync(m => m.MatchGuid == matchGuid))
        {
            var match = new Match { QLServerId = serverId };
            match.Apply(data, eventTime);
            db.Matches.Add(match);
            await db.SaveChangesAsync();
        }

        // Seed with a sentinel far enough in the past so that round-1 suicides
        // (before any ROUND_OVER) are counted immediately.
        _roundEndTimes[matchGuid] = -InterRoundWindowSeconds;

        var livePlayers = data.Players?
            .Where(p => !p.Bot)
            .Select(p => (p.SteamId, p.Name, p.Team == 1 ? "RED" : p.Team == 2 ? "BLUE" : ""));
        liveMatch.StartMatch(matchGuid, data.Map, data.GameType, data.ServerTitle, livePlayers);
    }

    private async Task HandleMatchReportAsync(MatchReportData data, int serverId, DateTime? eventTime, bool isReplay = false)
    {
        logger.LogInformation("Match report: {MatchGuid}, RED={Red} BLUE={Blue}, Aborted={Aborted}",
            data.MatchGuid, data.Tscore0, data.Tscore1, data.Aborted);

        await using var db = await dbFactory.CreateDbContextAsync();

        var matchGuid = data.MatchGuid.ToString();
        var match = await db.Matches.FirstOrDefaultAsync(m => m.MatchGuid == matchGuid);
        if (match is null)
        {
            logger.LogWarning("Match {MatchGuid} not found for report; creating it", matchGuid);
            match = new Match { QLServerId = serverId };
            db.Matches.Add(match);
        }

        match.Apply(data, eventTime);

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
        if (!isReplay)
        {
            standingsNotifier.NotifyStandingsChanged();
            await standingsService.SnapshotSeasonForMatchAsync(match.StartedAt);
        }
    }

    private async Task HandlePlayerStatsAsync(PlayerStatsData data)
    {
        if (data.Bot) return;

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
        var mp = await EnsureMatchPlayerAsync(db, match.Id, player.Id);

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
        var matchGuid = data.MatchGuid.ToString();

        if (liveMatch.Current is null && data.MatchGuid != Guid.Empty)
            liveMatch.StartMatch(matchGuid, "", "", "");

        liveMatch.RecordKill(
            data.Killer?.SteamId ?? "", data.Killer?.Name ?? "",
            data.Victim?.SteamId ?? "", data.Victim?.Name ?? "",
            data.Mod);

        // A kill is a suicide when the killer is absent/world (empty SteamId) or the same player as
        // the victim. The ZMQ SUICIDE flag is unreliable, so we derive it from the killer identity.
        var isSuicide = string.IsNullOrEmpty(data.Killer?.SteamId)
                        || data.Killer?.SteamId == data.Victim?.SteamId;

        // Suicides are only counted once a round has ended and at least InterRoundWindowSeconds
        // have elapsed since ROUND_OVER. This discards both respawn-phase deaths (within the
        // window) and pre-game deaths (no round end recorded yet).
        if (isSuicide
            && (!_roundEndTimes.TryGetValue(matchGuid, out var roundEndTime)
                || data.Time - roundEndTime < InterRoundWindowSeconds))
        {
            return;
        }

        if (data.Killer?.Bot == true || data.Victim?.Bot == true)
            return;

        await using var db = await dbFactory.CreateDbContextAsync();
        var match = await db.Matches.FirstOrDefaultAsync(m => m.MatchGuid == matchGuid);
        if (match is null) return;

        if (!isSuicide && !data.TeamKill
            && data.Killer?.SteamId is { Length: > 0 } killerSteamId)
        {
            var killer = await EnsurePlayerAsync(db, killerSteamId, data.Killer!.Name);
            var kmp = await EnsureMatchPlayerAsync(db, match.Id, killer.Id);
            kmp.Kills++;
            if (!string.IsNullOrEmpty(data.Killer.Weapon))
            {
                var weapon = data.Killer.Weapon;
                kmp.Weapons[weapon] = kmp.Weapons.TryGetValue(weapon, out var wk) ? wk + 1 : 1;
            }
        }

        if (data.Victim?.SteamId is { Length: > 0 } victimSteamId)
        {
            var victim = await EnsurePlayerAsync(db, victimSteamId, data.Victim!.Name);
            var vmp = await EnsureMatchPlayerAsync(db, match.Id, victim.Id);
            if (isSuicide)
                vmp.Suicides++;
            else
                vmp.Deaths++;
        }

        await db.SaveChangesAsync();
    }

    private async Task HandleRoundOverAsync(RoundOverData data)
    {
        var matchGuid = data.MatchGuid.ToString();

        // Record the round end time so inter-round suicides can be filtered
        _roundEndTimes[matchGuid] = data.Time;

        await using var db = await dbFactory.CreateDbContextAsync();
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
        if (data.Bot) return;

        liveMatch.RecordMedal(data.SteamId, data.Name, data.Medal, data.Total);

        await using var db = await dbFactory.CreateDbContextAsync();
        var matchGuid = data.MatchGuid.ToString();
        var match = await db.Matches.FirstOrDefaultAsync(m => m.MatchGuid == matchGuid);
        if (match is null) return;

        var player = await EnsurePlayerAsync(db, data.SteamId, data.Name);
        var mp = await EnsureMatchPlayerAsync(db, match.Id, player.Id);

        mp.Medals[data.Medal] = data.Total;
        await db.SaveChangesAsync();
    }

    private async Task HandlePlayerConnectAsync(PlayerConnectData data)
    {
        if (data.Bot) return;

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

    private static async Task<MatchPlayer> EnsureMatchPlayerAsync(AppDbContext db, int matchId, int playerId)
    {
        var mp = await db.MatchPlayers
            .FirstOrDefaultAsync(x => x.MatchId == matchId && x.PlayerId == playerId);
        if (mp is not null) return mp;

        mp = new MatchPlayer { MatchId = matchId, PlayerId = playerId };
        db.MatchPlayers.Add(mp);
        return mp;
    }
}
