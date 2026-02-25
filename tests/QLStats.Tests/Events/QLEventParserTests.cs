using QLStats.Events;
using Xunit;

namespace QLStats.Tests.Events;

public class QLEventParserTests
{
    private static readonly Guid TestGuid = new("11111111-1111-1111-1111-111111111111");

    // ── MATCH_STARTED ─────────────────────────────────────────────────────────

    [Fact]
    public void Parse_MatchStarted_ReturnsMatchStartedData()
    {
        var json = $$"""
            {
              "TYPE": "MATCH_STARTED",
              "DATA": {
                "MATCH_GUID": "{{TestGuid}}",
                "TIME": 0,
                "WARMUP": false,
                "MAP": "campgrounds",
                "GAME_TYPE": "CA",
                "SERVER_TITLE": "TestServer",
                "PLAYERS": []
              }
            }
            """;

        var result = QLEventParser.Parse(json)?.GetEvent();

        var e = Assert.IsType<MatchStartedData>(result);
        Assert.Equal(TestGuid, e.MatchGuid);
        Assert.Equal("campgrounds", e.Map);
        Assert.Equal("CA", e.GameType);
        Assert.Equal("TestServer", e.ServerTitle);
        Assert.False(e.Warmup);
    }

    [Fact]
    public void Parse_MatchStarted_WithPlayers_DeserializesPlayers()
    {
        var json = $$"""
            {
              "TYPE": "MATCH_STARTED",
              "DATA": {
                "MATCH_GUID": "{{TestGuid}}",
                "TIME": 0,
                "WARMUP": false,
                "MAP": "campgrounds",
                "GAME_TYPE": "CA",
                "SERVER_TITLE": "S",
                "PLAYERS": [
                  { "STEAM_ID": "12345", "NAME": "PlayerOne", "TEAM": 1 },
                  { "STEAM_ID": "67890", "NAME": "PlayerTwo", "TEAM": 2 }
                ]
              }
            }
            """;

        var e = Assert.IsType<MatchStartedData>(QLEventParser.Parse(json)?.GetEvent());

        Assert.NotNull(e.Players);
        Assert.Equal(2, e.Players.Count);
        Assert.Equal("12345", e.Players[0].SteamId);
        Assert.Equal("PlayerOne", e.Players[0].Name);
        Assert.Equal(1, e.Players[0].Team);
    }

    // ── MATCH_REPORT ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_MatchReport_ReturnsMatchReportData()
    {
        var json = $$"""
            {
              "TYPE": "MATCH_REPORT",
              "DATA": {
                "MATCH_GUID": "{{TestGuid}}",
                "TIME": 600,
                "WARMUP": false,
                "MAP": "campgrounds",
                "GAME_TYPE": "CA",
                "SERVER_TITLE": "S",
                "GAME_LENGTH": 600,
                "TSCORE0": 8,
                "TSCORE1": 5,
                "ABORTED": false
              }
            }
            """;

        var e = Assert.IsType<MatchReportData>(QLEventParser.Parse(json)?.GetEvent());

        Assert.Equal(8, e.Tscore0);
        Assert.Equal(5, e.Tscore1);
        Assert.Equal(600L, e.GameLength);
        Assert.False(e.Aborted);
    }

    // ── PLAYER_KILL ───────────────────────────────────────────────────────────

    [Fact]
    public void Parse_PlayerKill_ReturnsPlayerKillData()
    {
        var json = $$"""
            {
              "TYPE": "PLAYER_KILL",
              "DATA": {
                "MATCH_GUID": "{{TestGuid}}",
                "TIME": 42,
                "WARMUP": false,
                "MOD": "ROCKET",
                "SUICIDE": false,
                "TEAMKILL": false,
                "KILLER": { "STEAM_ID": "111", "NAME": "Killer", "WEAPON": "ROCKET", "TEAM": 1 },
                "VICTIM": { "STEAM_ID": "222", "NAME": "Victim", "WEAPON": "NONE",   "TEAM": 2 }
              }
            }
            """;

        var e = Assert.IsType<PlayerKillData>(QLEventParser.Parse(json)?.GetEvent());

        Assert.NotNull(e.Killer);
        Assert.Equal("111", e.Killer.SteamId);
        Assert.Equal("Killer", e.Killer.Name);
        Assert.Equal("ROCKET", e.Killer.Weapon);
        Assert.NotNull(e.Victim);
        Assert.Equal("222", e.Victim.SteamId);
        Assert.Equal("ROCKET", e.Mod);
        Assert.False(e.Suicide);
        Assert.False(e.TeamKill);
    }

    [Fact]
    public void Parse_PlayerKill_SuicideFlag_Propagates()
    {
        var json = $$"""
            {
              "TYPE": "PLAYER_KILL",
              "DATA": {
                "MATCH_GUID": "{{TestGuid}}",
                "TIME": 10,
                "WARMUP": false,
                "MOD": "LAVA",
                "SUICIDE": true,
                "TEAMKILL": false
              }
            }
            """;

        var e = Assert.IsType<PlayerKillData>(QLEventParser.Parse(json)?.GetEvent());
        Assert.True(e.Suicide);
    }

    // QL ZMQ stats send SUICIDE/TEAMKILL as integers 0/1 rather than JSON booleans
    [Fact]
    public void Parse_PlayerKill_SuicideAsInteger_Propagates()
    {
        var json = $$"""
            {
              "TYPE": "PLAYER_KILL",
              "DATA": {
                "MATCH_GUID": "{{TestGuid}}",
                "TIME": 10,
                "WARMUP": false,
                "MOD": "LAVA",
                "SUICIDE": 1,
                "TEAMKILL": 0
              }
            }
            """;

        var e = Assert.IsType<PlayerKillData>(QLEventParser.Parse(json)?.GetEvent());
        Assert.True(e.Suicide);
        Assert.False(e.TeamKill);
    }

    // ── ROUND_OVER ────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_RoundOver_ReturnsRoundOverData()
    {
        var json = $$"""
            {
              "TYPE": "ROUND_OVER",
              "DATA": {
                "MATCH_GUID": "{{TestGuid}}",
                "TIME": 120,
                "WARMUP": false,
                "ROUND": 3,
                "TEAM_WON": "RED"
              }
            }
            """;

        var e = Assert.IsType<RoundOverData>(QLEventParser.Parse(json)?.GetEvent());

        Assert.Equal(3, e.Round);
        Assert.Equal("RED", e.TeamWon);
    }

    // ── PLAYER_STATS ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_PlayerStats_ReturnsPlayerStatsData()
    {
        var json = $$"""
            {
              "TYPE": "PLAYER_STATS",
              "DATA": {
                "MATCH_GUID": "{{TestGuid}}",
                "TIME": 600,
                "WARMUP": false,
                "STEAM_ID": "999",
                "NAME": "Frag",
                "TEAM": 1,
                "KILLS": 15,
                "DEATHS": 3,
                "WIN": 1,
                "ABORTED": false,
                "QUIT": 0,
                "DAMAGE": { "DEALT": 5000, "TAKEN": 1200 }
              }
            }
            """;

        var e = Assert.IsType<PlayerStatsData>(QLEventParser.Parse(json)?.GetEvent());

        Assert.Equal("999", e.SteamId);
        Assert.Equal(15, e.Kills);
        Assert.Equal(3, e.Deaths);
        Assert.Equal(1, e.Win);
        Assert.NotNull(e.Damage);
        Assert.Equal(5000, e.Damage.Dealt);
        Assert.Equal(1200, e.Damage.Taken);
    }

    // ── PLAYER_CONNECT ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_PlayerConnect_ReturnsPlayerConnectData()
    {
        var json = $$"""
            {
              "TYPE": "PLAYER_CONNECT",
              "DATA": {
                "MATCH_GUID": "{{TestGuid}}",
                "TIME": 0,
                "WARMUP": false,
                "STEAM_ID": "777",
                "NAME": "Connector"
              }
            }
            """;

        var e = Assert.IsType<PlayerConnectData>(QLEventParser.Parse(json)?.GetEvent());

        Assert.Equal("777", e.SteamId);
        Assert.Equal("Connector", e.Name);
    }

    // ── PLAYER_MEDAL ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_PlayerMedal_ReturnsPlayerMedalData()
    {
        var json = $$"""
            {
              "TYPE": "PLAYER_MEDAL",
              "DATA": {
                "MATCH_GUID": "{{TestGuid}}",
                "TIME": 300,
                "WARMUP": false,
                "STEAM_ID": "555",
                "NAME": "MedalWinner",
                "MEDAL": "Excellent",
                "TOTAL": 2
              }
            }
            """;

        var e = Assert.IsType<PlayerMedalData>(QLEventParser.Parse(json)?.GetEvent());

        Assert.Equal("555", e.SteamId);
        Assert.Equal("Excellent", e.Medal);
        Assert.Equal(2, e.Total);
    }

    // ── Warmup propagation ────────────────────────────────────────────────────

    [Fact]
    public void Parse_Warmup_True_Propagates()
    {
        var json = $$"""
            {
              "TYPE": "PLAYER_KILL",
              "DATA": {
                "MATCH_GUID": "{{TestGuid}}",
                "TIME": 5,
                "WARMUP": true,
                "MOD": "ROCKET",
                "SUICIDE": false,
                "TEAMKILL": false
              }
            }
            """;

        var e = Assert.IsType<PlayerKillData>(QLEventParser.Parse(json)?.GetEvent());
        Assert.True(e.Warmup);
    }

    // ── Unknown / missing TYPE ────────────────────────────────────────────────

    [Fact]
    public void Parse_UnknownType_ReturnsNull()
    {
        var json = """{ "TYPE": "UNKNOWN_EVENT", "DATA": {} }""";

        Assert.Null(QLEventParser.Parse(json));
    }

    [Fact]
    public void Parse_NoTypeDiscriminator_ReturnsNull()
    {
        Assert.Null(QLEventParser.Parse("{}"));
    }
}
