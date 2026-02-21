using System.Text.Json;
using System.Text.Json.Serialization;

namespace QLStats.Services;

public abstract record QlEvent
{
    public Guid MatchGuid { get; init; }
    public int Time { get; init; }
    public bool Warmup { get; init; }
}

// MATCH_STARTED
public record MatchStartedPlayer
{
    public string SteamId { get; init; } = "";
    public string Name { get; init; } = "";
    public int Team { get; init; }
}

public record MatchStartedData : QlEvent
{
    public string Map { get; init; } = "";
    public string GameType { get; init; } = "";
    public string ServerTitle { get; init; } = "";
    public List<MatchStartedPlayer>? Players { get; init; }
}

// MATCH_REPORT
public record MatchReportData : QlEvent
{
    public string Map { get; init; } = "";
    public string GameType { get; init; } = "";
    public string ServerTitle { get; init; } = "";
    public long GameLength { get; init; }
    [JsonPropertyName("TSCORE0")] public int Tscore0 { get; init; }
    [JsonPropertyName("TSCORE1")] public int Tscore1 { get; init; }
    public bool Aborted { get; init; }
}

// PLAYER_KILL
public record PlayerActor
{
    public string SteamId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Weapon { get; init; } = "";
    public int Team { get; init; }
}

public record PlayerKillData : QlEvent
{
    public PlayerActor? Killer { get; init; }
    public PlayerActor? Victim { get; init; }
    public string Mod { get; init; } = "";
    public bool Suicide { get; init; }
    [JsonPropertyName("TEAMKILL")] public bool TeamKill { get; init; }
}

// ROUND_OVER
public record RoundOverData : QlEvent
{
    public int Round { get; init; }
    public string TeamWon { get; init; } = "";
}

// PLAYER_STATS
public record DamageStats
{
    public int Dealt { get; init; }
    public int Taken { get; init; }
}

public record PlayerStatsData : QlEvent
{
    public string SteamId { get; init; } = "";
    public string Name { get; init; } = "";
    public int Team { get; init; }
    public int Kills { get; init; }
    public int Deaths { get; init; }
    public DamageStats? Damage { get; init; }
    public int Win { get; init; }
    public bool Aborted { get; init; }
    public int Quit { get; init; }
}

// PLAYER_CONNECT
public record PlayerConnectData : QlEvent
{
    public string SteamId { get; init; } = "";
    public string Name { get; init; } = "";
}

// PLAYER_MEDAL
public record PlayerMedalData : QlEvent
{
    public string SteamId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Medal { get; init; } = "";
    public int Total { get; init; }
}

public static class QLEventParser
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper
    };

    public static (string EventType, JsonElement Data) ParseEnvelope(string rawJson)
    {
        var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        var type = root.GetProperty("TYPE").GetString() ?? "";
        var data = root.GetProperty("DATA");
        return (type, data);
    }

    public static MatchStartedData?  ParseMatchStarted(JsonElement data)  => Deserialize<MatchStartedData>(data);
    public static MatchReportData?   ParseMatchReport(JsonElement data)    => Deserialize<MatchReportData>(data);
    public static PlayerKillData?    ParsePlayerKill(JsonElement data)     => Deserialize<PlayerKillData>(data);
    public static RoundOverData?     ParseRoundOver(JsonElement data)      => Deserialize<RoundOverData>(data);
    public static PlayerStatsData?   ParsePlayerStats(JsonElement data)    => Deserialize<PlayerStatsData>(data);
    public static PlayerConnectData? ParsePlayerConnect(JsonElement data)  => Deserialize<PlayerConnectData>(data);
    public static PlayerMedalData?   ParsePlayerMedal(JsonElement data)    => Deserialize<PlayerMedalData>(data);

    private static T? Deserialize<T>(JsonElement data) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(data, JsonSerializerOptions); }
        catch { return null; }
    }
}
