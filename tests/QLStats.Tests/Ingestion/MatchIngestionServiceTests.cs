using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QLStats.Data;
using QLStats.Data.Entities;
using QLStats.Events;
using QLStats.Services;
using Xunit;

namespace QLStats.Tests.Ingestion;

public class MatchIngestionServiceTests
{
    private static readonly Guid MatchGuid = new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private const int ServerId = 1;

    private static (MatchIngestionService Sut, LiveMatchState Live, DbContextOptions<AppDbContext> Options) CreateSut()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var seed = new AppDbContext(options);
        seed.QLServers.Add(new QLServer { Id = ServerId, ZmqAddress = "tcp://localhost:27960" });
        seed.SaveChanges();

        var live = new LiveMatchState();
        var sut = new MatchIngestionService(
            new TestDbContextFactory(options), live,
            new StandingsNotifier(),
            new StandingsService(new TestDbContextFactory(options), NullLogger<StandingsService>.Instance),
            NullLogger<MatchIngestionService>.Instance);

        return (sut, live, options);
    }

    // ── Full CA match simulation ───────────────────────────────────────────────

    [Fact]
    public async Task FullCaMatch_2v2_ProducesCorrectDbState()
    {
        var (sut, _, options) = CreateSut();

        // MATCH_STARTED
        await sut.HandleAsync(new MatchStartedData
        {
            MatchGuid = MatchGuid,
            Map = "campgrounds",
            GameType = "CA",
            ServerTitle = "TestServer",
            Players =
            [
                new() { SteamId = "111", Name = "RedOne",  Team = 1 },
                new() { SteamId = "222", Name = "RedTwo",  Team = 1 },
                new() { SteamId = "333", Name = "BlueOne", Team = 2 },
                new() { SteamId = "444", Name = "BlueTwo", Team = 2 },
            ]
        }, ServerId);

        // ── Round 1: three RED kills ───────────────────────────────────────────
        await sut.HandleAsync(Kill("111", "RedOne",  "333", "BlueOne", "ROCKET"),  ServerId);
        await sut.HandleAsync(Kill("111", "RedOne",  "444", "BlueTwo", "ROCKET"),  ServerId);
        await sut.HandleAsync(Kill("222", "RedTwo",  "333", "BlueOne", "RAILGUN"), ServerId);
        await sut.HandleAsync(RoundOver(1, "RED", time: 60), ServerId);

        // ── Round 2: two BLUE kills then one RED kill ─────────────────────────
        await sut.HandleAsync(Kill("333", "BlueOne", "111", "RedOne",  "ROCKET"),  ServerId);
        await sut.HandleAsync(Kill("444", "BlueTwo", "222", "RedTwo",  "RAILGUN"), ServerId);
        await sut.HandleAsync(Kill("111", "RedOne",  "333", "BlueOne", "PLASMA"),  ServerId);
        await sut.HandleAsync(RoundOver(2, "RED", time: 130), ServerId);

        // MATCH_REPORT — RED wins 2-0
        await sut.HandleAsync(new MatchReportData
        {
            MatchGuid = MatchGuid,
            Map = "campgrounds",
            GameType = "CA",
            ServerTitle = "TestServer",
            Tscore0 = 2,
            Tscore1 = 0,
            GameLength = 130,
        }, ServerId);

        // PLAYER_STATS (arrive after MATCH_REPORT in normal QL flow)
        await sut.HandleAsync(Stats("111", "RedOne",  team: 1, win: 1, kills: 3, deaths: 1, dealt: 4000, taken:  800), ServerId);
        await sut.HandleAsync(Stats("222", "RedTwo",  team: 1, win: 1, kills: 1, deaths: 1, dealt: 2500, taken: 1000), ServerId);
        await sut.HandleAsync(Stats("333", "BlueOne", team: 2, win: 0, kills: 1, deaths: 3, dealt:  800, taken: 4000), ServerId);
        await sut.HandleAsync(Stats("444", "BlueTwo", team: 2, win: 0, kills: 1, deaths: 1, dealt: 1000, taken: 2500), ServerId);

        // ── Assertions ────────────────────────────────────────────────────────
        await using var db = new AppDbContext(options);
        var match = await db.Matches
            .Include(m => m.MatchPlayers).ThenInclude(mp => mp.Player)
            .Include(m => m.RoundResults)
            .FirstAsync();

        // Match metadata
        Assert.Equal(MatchGuid.ToString(), match.MatchGuid);
        Assert.Equal("campgrounds", match.Map);
        Assert.Equal("CA", match.GameType);
        Assert.NotNull(match.FinishedAt);
        Assert.Equal(2, match.TeamRedRounds);
        Assert.Equal(0, match.TeamBlueRounds);

        // Rounds
        Assert.Equal(2, match.RoundResults.Count);
        Assert.All(match.RoundResults, r => Assert.Equal("RED", r.TeamWon));

        // All four players have MatchPlayer rows
        Assert.Equal(4, match.MatchPlayers.Count);
        var mp = match.MatchPlayers.ToDictionary(x => x.Player.SteamId);

        // RedOne: 3 kills across both rounds, 1 death, won
        Assert.Equal("RED",  mp["111"].Team);
        Assert.Equal(3,      mp["111"].Kills);
        Assert.Equal(1,      mp["111"].Deaths);
        Assert.True(         mp["111"].Won);
        Assert.Equal(4000,   mp["111"].DamageDealt);
        Assert.Equal(800,    mp["111"].DamageTaken);
        Assert.Equal(2,      mp["111"].RoundsWon);
        Assert.Equal(0,      mp["111"].RoundsLost);

        // RedTwo: 1 kill, 1 death, won
        Assert.Equal("RED",  mp["222"].Team);
        Assert.Equal(1,      mp["222"].Kills);
        Assert.Equal(1,      mp["222"].Deaths);
        Assert.True(         mp["222"].Won);
        Assert.Equal(2,      mp["222"].RoundsWon);

        // BlueOne: 1 kill, 3 deaths, lost
        Assert.Equal("BLUE", mp["333"].Team);
        Assert.Equal(1,      mp["333"].Kills);
        Assert.Equal(3,      mp["333"].Deaths);
        Assert.False(        mp["333"].Won);
        Assert.Equal(0,      mp["333"].RoundsWon);
        Assert.Equal(2,      mp["333"].RoundsLost);

        // BlueTwo: 1 kill, 1 death, lost
        Assert.Equal("BLUE", mp["444"].Team);
        Assert.Equal(1,      mp["444"].Kills);
        Assert.Equal(1,      mp["444"].Deaths);
        Assert.False(        mp["444"].Won);
    }

    // ── Suicide counting (round-based) ────────────────────────────────────────

    [Fact]
    public async Task Suicide_DuringRoundOne_IsCounted()
    {
        var (sut, _, options) = CreateSut();

        await sut.HandleAsync(new MatchStartedData
        {
            MatchGuid = MatchGuid, Map = "lostworld", GameType = "CA", ServerTitle = "S",
            Players = [new() { SteamId = "111", Name = "P1", Team = 1 }]
        }, ServerId);

        // No ROUND_OVER yet — but MATCH_STARTED seeds the sentinel, so round-1
        // suicides must be counted.
        await sut.HandleAsync(new PlayerKillData
        {
            MatchGuid = MatchGuid, Time = 30,
            Victim = new() { SteamId = "111", Name = "P1", Weapon = "NONE", Team = 1 }
        }, ServerId);

        await using var db = new AppDbContext(options);
        var entry = await db.MatchPlayers
            .Include(x => x.Player)
            .SingleAsync(x => x.Player.SteamId == "111");

        Assert.Equal(1, entry.Suicides);
    }

    [Fact]
    public async Task Suicide_WithinInterRoundWindow_IsNotCounted()
    {
        var (sut, _, options) = CreateSut();

        await sut.HandleAsync(new MatchStartedData
        {
            MatchGuid = MatchGuid, Map = "lostworld", GameType = "CA", ServerTitle = "S",
            Players = [new() { SteamId = "111", Name = "P1", Team = 1 }]
        }, ServerId);

        await sut.HandleAsync(RoundOver(1, "RED", time: 60), ServerId);

        // Suicide 5 s after round end — inside the 11 s window → filtered
        await sut.HandleAsync(new PlayerKillData
        {
            MatchGuid = MatchGuid, Time = 65,
            Victim = new() { SteamId = "111", Name = "P1", Weapon = "NONE", Team = 1 }
        }, ServerId);

        // Suicide 15 s after round end — outside window → counted
        await sut.HandleAsync(new PlayerKillData
        {
            MatchGuid = MatchGuid, Time = 75,
            Victim = new() { SteamId = "111", Name = "P1", Weapon = "NONE", Team = 1 }
        }, ServerId);

        await using var db = new AppDbContext(options);
        var entry = await db.MatchPlayers
            .Include(x => x.Player)
            .SingleAsync(x => x.Player.SteamId == "111");

        Assert.Equal(1, entry.Suicides);
    }

    // ── Match report without prior MATCH_STARTED ──────────────────────────────

    [Fact]
    public async Task MissingMatchStarted_MatchReportCreatesMatch()
    {
        var (sut, _, options) = CreateSut();

        await sut.HandleAsync(new MatchReportData
        {
            MatchGuid = MatchGuid,
            Map = "thunderstruck",
            GameType = "CA",
            ServerTitle = "S",
            Tscore0 = 3,
            Tscore1 = 1,
        }, ServerId);

        await using var db = new AppDbContext(options);
        var match = await db.Matches.FirstAsync();
        Assert.Equal("thunderstruck", match.Map);
        Assert.Equal(3, match.TeamRedRounds);
        Assert.Equal(1, match.TeamBlueRounds);
        Assert.NotNull(match.FinishedAt);
    }

    // ── LiveMatchState updated throughout a match ─────────────────────────────

    [Fact]
    public async Task LiveMatchState_UpdatedThroughoutMatch()
    {
        var (sut, live, _) = CreateSut();

        await sut.HandleAsync(new MatchStartedData
        {
            MatchGuid = MatchGuid, Map = "aerowalk", GameType = "CA", ServerTitle = "S",
            Players =
            [
                new() { SteamId = "111", Name = "A", Team = 1 },
                new() { SteamId = "222", Name = "B", Team = 2 },
            ]
        }, ServerId);

        Assert.NotNull(live.Current);
        Assert.Equal("aerowalk", live.Current!.Map);
        Assert.Equal(2, live.Current.Players.Count);

        await sut.HandleAsync(Kill("111", "A", "222", "B", "ROCKET"), ServerId);
        Assert.Equal(1, live.Current.Players.Single(p => p.SteamId == "111").Kills);

        await sut.HandleAsync(RoundOver(1, "RED", time: 30), ServerId);
        Assert.Equal(1, live.Current.RedScore);
        Assert.Equal(1, live.Current.RoundNumber);

        await sut.HandleAsync(new MatchReportData
        {
            MatchGuid = MatchGuid, Map = "aerowalk", GameType = "CA", ServerTitle = "S",
            Tscore0 = 1, Tscore1 = 0,
        }, ServerId);

        Assert.Null(live.Current); // ended
    }

    // ── Bot filtering ─────────────────────────────────────────────────────────

    [Fact]
    public async Task BotPlayer_Stats_NotIngested()
    {
        var (sut, _, options) = CreateSut();

        await sut.HandleAsync(new MatchStartedData
        {
            MatchGuid = MatchGuid, Map = "campgrounds", GameType = "CA", ServerTitle = "S"
        }, ServerId);

        await sut.HandleAsync(new PlayerStatsData
        {
            MatchGuid = MatchGuid, SteamId = "bot1", Name = "Ranger", Bot = true,
            Team = 1, Win = 1, Damage = new() { Dealt = 500, Taken = 200 }
        }, ServerId);

        await using var db = new AppDbContext(options);
        Assert.Empty(db.Players.ToList());
        Assert.Empty(db.MatchPlayers.ToList());
    }

    [Fact]
    public async Task BotPlayer_Kill_NotCounted()
    {
        var (sut, _, options) = CreateSut();

        await sut.HandleAsync(new MatchStartedData
        {
            MatchGuid = MatchGuid, Map = "campgrounds", GameType = "CA", ServerTitle = "S"
        }, ServerId);

        // Establish the human player via PLAYER_STATS before bot-kill events
        await sut.HandleAsync(new PlayerStatsData
        {
            MatchGuid = MatchGuid, SteamId = "111", Name = "Human",
            Team = 1, Win = 0, Damage = new() { Dealt = 100, Taken = 50 }
        }, ServerId);

        // Bot kills a human — human should not receive a death
        await sut.HandleAsync(new PlayerKillData
        {
            MatchGuid = MatchGuid,
            Killer = new() { SteamId = "bot1", Name = "Ranger", Weapon = "ROCKET", Bot = true },
            Victim = new() { SteamId = "111",  Name = "Human",  Weapon = "NONE" },
            Mod = "ROCKET"
        }, ServerId);

        // Human kills a bot — human should not receive a kill credit
        await sut.HandleAsync(new PlayerKillData
        {
            MatchGuid = MatchGuid,
            Killer = new() { SteamId = "111", Name = "Human", Weapon = "ROCKET" },
            Victim = new() { SteamId = "bot1", Name = "Ranger", Weapon = "NONE", Bot = true },
            Mod = "ROCKET"
        }, ServerId);

        await using var db2 = new AppDbContext(options);
        var human = db2.Players.FirstOrDefault(p => p.SteamId == "111");
        var bot   = db2.Players.FirstOrDefault(p => p.SteamId == "bot1");

        Assert.NotNull(human);
        Assert.Null(bot); // bot player record never created

        var humanMp = db2.MatchPlayers.FirstOrDefault(mp => mp.PlayerId == human!.Id);
        Assert.NotNull(humanMp);
        Assert.Equal(0, humanMp.Kills);  // killing a bot not credited
        Assert.Equal(0, humanMp.Deaths); // dying to a bot not recorded
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static PlayerKillData Kill(
        string killerSteam, string killerName,
        string victimSteam, string victimName,
        string weapon) => new()
    {
        MatchGuid = MatchGuid,
        Killer = new() { SteamId = killerSteam, Name = killerName, Weapon = weapon, Team = 0 },
        Victim = new() { SteamId = victimSteam, Name = victimName, Weapon = "NONE",  Team = 0 },
        Mod    = weapon,
    };

    private static RoundOverData RoundOver(int round, string winner, int time) => new()
    {
        MatchGuid = MatchGuid,
        Round     = round,
        TeamWon   = winner,
        Time      = time,
    };

    private static PlayerStatsData Stats(
        string steamId, string name,
        int team, int win, int dealt, int taken,
        int kills = 0, int deaths = 0) => new()
    {
        MatchGuid = MatchGuid,
        SteamId   = steamId,
        Name      = name,
        Team      = team,
        Win       = win,
        Kills     = kills,
        Deaths    = deaths,
        Damage    = new() { Dealt = dealt, Taken = taken },
    };

    // ── TestDbContextFactory ──────────────────────────────────────────────────

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options)
        : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);
    }
}
