using System.Text.Json.Serialization;

namespace QLStats.Events;

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
    public bool Bot { get; init; }
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
    public bool Bot { get; init; }
}

public record PlayerKillData : QlEvent
{
    public PlayerActor? Killer { get; init; }
    public PlayerActor? Victim { get; init; }
    public string Mod { get; init; } = "";
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
    public bool Bot { get; init; }
}

// PLAYER_CONNECT
public record PlayerConnectData : QlEvent
{
    public string SteamId { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Bot { get; init; }
}

// PLAYER_MEDAL
public record PlayerMedalData : QlEvent
{
    public string SteamId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Medal { get; init; } = "";
    public int Total { get; init; }
    public bool Bot { get; init; }
}

public abstract record QlEnvelope
{
    public abstract QlEvent? GetEvent();
}

public record QlEnvelope<TData> : QlEnvelope where TData : QlEvent
{
    public TData? Data { get; init; }
    public override QlEvent? GetEvent() => Data;
}
