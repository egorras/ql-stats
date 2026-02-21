using System.Text.Json;

namespace QLStats.Services;

// Strongly-typed event records

public record MatchStartedData(
    string MatchGuid,
    string Map,
    string GameType,
    string ServerTitle,
    long Time
);

public record MatchReportData(
    string MatchGuid,
    string Map,
    string GameType,
    string ServerTitle,
    long MatchDuration,
    int Tscore0,   // RED score
    int Tscore1,   // BLUE score
    bool Aborted
);

public record PlayerKillData(
    string KillerSteamId,
    string VictimSteamId,
    string KillerName,
    string VictimName,
    string Mod,
    string KillerWeapon,
    int Time,
    bool Suicide,
    bool TeamKill,
    bool Warmup
);

public record RoundOverData(
    string MatchGuid,
    int RoundNumber,
    string WinningTeam,  // "RED" or "BLUE"
    int RoundTime
);

public record PlayerStatsData(
    string MatchGuid,
    string SteamId,
    string Name,
    int Team,      // 1=RED, 2=BLUE, 0=FREE/DUEL
    int Kills,
    int Deaths,
    int DamageDealt,
    int DamageTaken,
    bool Won,
    bool Warmup,
    bool Aborted,
    bool Quit
);

public record PlayerConnectData(
    string MatchGuid,
    string SteamId,
    string Name,
    bool Warmup
);

public record PlayerMedalData(
    string MatchGuid,
    string SteamId,
    string Name,
    string Medal,
    int Total,
    bool Warmup
);

public static class QLEventParser
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static (string EventType, JsonElement Data) ParseEnvelope(string rawJson)
    {
        var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        var type = root.GetProperty("TYPE").GetString() ?? "";
        var data = root.GetProperty("DATA");
        return (type, data);
    }

    public static MatchStartedData? ParseMatchStarted(JsonElement data)
    {
        try
        {
            var guid = GetString(data, "MATCH_GUID", "MATCHGUID") ?? Guid.NewGuid().ToString();
            var map = GetString(data, "MAP") ?? "";
            var gameType = GetString(data, "GAME_TYPE", "GAMETYPE") ?? "";
            var serverTitle = GetString(data, "SERVER_TITLE", "SERVERDOMAIN", "sv_hostname") ?? "";
            var time = GetLong(data, "TIME") ?? 0;
            return new MatchStartedData(guid, map, gameType, serverTitle, time);
        }
        catch { return null; }
    }

    public static MatchReportData? ParseMatchReport(JsonElement data)
    {
        try
        {
            var guid = GetString(data, "MATCH_GUID", "MATCHGUID") ?? "";
            var map = GetString(data, "MAP") ?? "";
            var gameType = GetString(data, "GAME_TYPE", "GAMETYPE") ?? "";
            var serverTitle = GetString(data, "SERVER_TITLE", "SERVERDOMAIN", "sv_hostname") ?? "";
            var duration = GetLong(data, "GAME_LENGTH", "MATCHDURATION", "DURATION") ?? 0;
            var tscore0 = GetInt(data, "TSCORE0") ?? 0;
            var tscore1 = GetInt(data, "TSCORE1") ?? 0;
            var aborted = GetBool(data, "ABORTED");

            return new MatchReportData(guid, map, gameType, serverTitle, duration, tscore0, tscore1, aborted);
        }
        catch { return null; }
    }

    public static PlayerKillData? ParsePlayerKill(JsonElement data)
    {
        try
        {
            string killerSteamId, killerName, killerWeapon;
            if (data.TryGetProperty("KILLER", out var killerEl) && killerEl.ValueKind == JsonValueKind.Object)
            {
                killerSteamId = GetString(killerEl, "STEAM_ID") ?? "";
                killerName = GetString(killerEl, "NAME") ?? "";
                killerWeapon = GetString(killerEl, "WEAPON") ?? "";
            }
            else
            {
                var flat = GetString(data, "KILLER") ?? "";
                killerSteamId = flat; killerName = flat; killerWeapon = "";
            }

            string victimSteamId, victimName;
            if (data.TryGetProperty("VICTIM", out var victimEl) && victimEl.ValueKind == JsonValueKind.Object)
            {
                victimSteamId = GetString(victimEl, "STEAM_ID") ?? "";
                victimName = GetString(victimEl, "NAME") ?? "";
            }
            else
            {
                var flat = GetString(data, "VICTIM") ?? "";
                victimSteamId = flat; victimName = flat;
            }

            var mod = GetString(data, "MOD") ?? "";
            var time = GetInt(data, "TIME") ?? 0;
            var suicide = GetBool(data, "SUICIDE");
            var teamKill = GetBool(data, "TEAMKILL");
            var warmup = GetBool(data, "WARMUP");

            return new PlayerKillData(killerSteamId, victimSteamId, killerName, victimName,
                mod, killerWeapon, time, suicide, teamKill, warmup);
        }
        catch { return null; }
    }

    public static RoundOverData? ParseRoundOver(JsonElement data)
    {
        try
        {
            var guid = GetString(data, "MATCH_GUID", "MATCHGUID") ?? "";
            var round = GetInt(data, "ROUND") ?? 0;
            var teamWon = GetString(data, "TEAM_WON") ?? "RED";
            var time = GetInt(data, "TIME", "ROUNDTIME") ?? 0;
            return new RoundOverData(guid, round, teamWon.ToUpperInvariant(), time);
        }
        catch { return null; }
    }

    public static PlayerStatsData? ParsePlayerStats(JsonElement data)
    {
        try
        {
            var guid = GetString(data, "MATCH_GUID") ?? "";
            var steamId = GetString(data, "STEAM_ID") ?? "";
            var name = GetString(data, "NAME") ?? "";
            var team = GetInt(data, "TEAM") ?? 0;
            var kills = GetInt(data, "KILLS") ?? 0;
            var deaths = GetInt(data, "DEATHS") ?? 0;
            var win = GetInt(data, "WIN") ?? 0;
            var warmup = GetBool(data, "WARMUP");
            var aborted = GetBool(data, "ABORTED");
            var quit = GetInt(data, "QUIT") ?? 0;

            int dealt = 0, taken = 0;
            if (data.TryGetProperty("DAMAGE", out var dmg))
            {
                dealt = GetInt(dmg, "DEALT") ?? 0;
                taken = GetInt(dmg, "TAKEN") ?? 0;
            }

            return new PlayerStatsData(guid, steamId, name, team, kills, deaths,
                dealt, taken, win == 1, warmup, aborted, quit == 1);
        }
        catch { return null; }
    }

    public static PlayerConnectData? ParsePlayerConnect(JsonElement data)
    {
        try
        {
            var guid = GetString(data, "MATCH_GUID") ?? "";
            var steamId = GetString(data, "STEAM_ID") ?? "";
            var name = GetString(data, "NAME") ?? "";
            var warmup = GetBool(data, "WARMUP");
            return new PlayerConnectData(guid, steamId, name, warmup);
        }
        catch { return null; }
    }

    public static PlayerMedalData? ParsePlayerMedal(JsonElement data)
    {
        try
        {
            var guid = GetString(data, "MATCH_GUID") ?? "";
            var steamId = GetString(data, "STEAM_ID") ?? "";
            var name = GetString(data, "NAME") ?? "";
            var medal = GetString(data, "MEDAL") ?? "";
            var total = GetInt(data, "TOTAL") ?? 0;
            var warmup = GetBool(data, "WARMUP");
            return new PlayerMedalData(guid, steamId, name, medal, total, warmup);
        }
        catch { return null; }
    }

    private static string? GetString(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString();
        return null;
    }

    private static int? GetInt(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
                if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var si)) return si;
            }
        return null;
    }

    private static long? GetLong(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var i)) return i;
                if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out var si)) return si;
            }
        return null;
    }

    private static bool GetBool(JsonElement el, params string[] keys)
    {
        foreach (var k in keys)
            if (el.TryGetProperty(k, out var v))
            {
                if (v.ValueKind == JsonValueKind.True) return true;
                if (v.ValueKind == JsonValueKind.False) return false;
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i != 0;
            }
        return false;
    }
}
