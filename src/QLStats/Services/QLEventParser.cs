using System.Text.Json;
using System.Text.Json.Serialization;

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
    List<MatchReportPlayer> Players
);

public record MatchReportPlayer(
    string SteamId,
    string Name,
    int Team,      // 1=RED, 2=BLUE
    int Kills,
    int Deaths,
    int DamageDealt
);

public record PlayerKillData(
    string KillerSteamId,
    string VictimSteamId,
    string KillerName,
    string VictimName,
    string Weapon,
    int Time
);

public record RoundOverData(
    string MatchGuid,
    int RoundNumber,
    int WinningTeam,  // 1=RED, 2=BLUE
    int RoundTime
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
            var guid = GetString(data, "MATCHGUID", "match_guid") ?? Guid.NewGuid().ToString();
            var map = GetString(data, "MAP", "map") ?? "";
            var gameType = GetString(data, "GAMETYPE", "game_type") ?? "CA";
            var serverTitle = GetString(data, "SERVERDOMAIN", "sv_hostname", "server_title") ?? "";
            var time = GetLong(data, "TIME", "time") ?? 0;
            return new MatchStartedData(guid, map, gameType, serverTitle, time);
        }
        catch { return null; }
    }

    public static MatchReportData? ParseMatchReport(JsonElement data)
    {
        try
        {
            var guid = GetString(data, "MATCHGUID", "match_guid") ?? "";
            var map = GetString(data, "MAP", "map") ?? "";
            var gameType = GetString(data, "GAMETYPE", "game_type") ?? "CA";
            var serverTitle = GetString(data, "SERVERDOMAIN", "sv_hostname", "server_title") ?? "";
            var duration = GetLong(data, "MATCHDURATION", "DURATION") ?? 0;
            var tscore0 = GetInt(data, "TSCORE0", "tscore0") ?? 0;
            var tscore1 = GetInt(data, "TSCORE1", "tscore1") ?? 0;

            var players = new List<MatchReportPlayer>();
            if (data.TryGetProperty("PLAYERS", out var playersEl) ||
                data.TryGetProperty("players", out playersEl))
            {
                foreach (var p in playersEl.EnumerateArray())
                {
                    var steamId = GetString(p, "STEAMID", "steam_id") ?? "";
                    var name = GetString(p, "NAME", "name") ?? "";
                    var team = GetInt(p, "TEAM", "team") ?? 1;
                    var kills = GetInt(p, "KILLS", "kills") ?? 0;
                    var deaths = GetInt(p, "DEATHS", "deaths") ?? 0;
                    var dmg = GetInt(p, "DAMAGE_DEALT", "DAMAGEDONE", "damage_dealt") ?? 0;
                    players.Add(new MatchReportPlayer(steamId, name, team, kills, deaths, dmg));
                }
            }

            return new MatchReportData(guid, map, gameType, serverTitle, duration, tscore0, tscore1, players);
        }
        catch { return null; }
    }

    public static PlayerKillData? ParsePlayerKill(JsonElement data)
    {
        try
        {
            var killer = GetString(data, "KILLER", "killer") ?? "";
            var victim = GetString(data, "VICTIM", "victim") ?? "";

            // KILLER / VICTIM can be nested objects or flat strings
            string killerSteamId, killerName, victimSteamId, victimName;
            if (data.TryGetProperty("KILLER", out var killerEl) && killerEl.ValueKind == JsonValueKind.Object)
            {
                killerSteamId = GetString(killerEl, "STEAM_ID", "steam_id") ?? "";
                killerName = GetString(killerEl, "NAME", "name") ?? "";
            }
            else { killerSteamId = killer; killerName = killer; }

            if (data.TryGetProperty("VICTIM", out var victimEl) && victimEl.ValueKind == JsonValueKind.Object)
            {
                victimSteamId = GetString(victimEl, "STEAM_ID", "steam_id") ?? "";
                victimName = GetString(victimEl, "NAME", "name") ?? "";
            }
            else { victimSteamId = victim; victimName = victim; }

            var weapon = GetString(data, "MOD", "weapon") ?? "";
            var time = GetInt(data, "TIME", "time") ?? 0;

            return new PlayerKillData(killerSteamId, victimSteamId, killerName, victimName, weapon, time);
        }
        catch { return null; }
    }

    public static RoundOverData? ParseRoundOver(JsonElement data)
    {
        try
        {
            var guid = GetString(data, "MATCHGUID", "match_guid") ?? "";
            var round = GetInt(data, "ROUND", "round_number") ?? 0;
            var winner = GetInt(data, "TEAM_WON", "team_won", "WINNER") ?? 1;
            var time = GetInt(data, "ROUNDTIME", "round_time") ?? 0;
            return new RoundOverData(guid, round, winner, time);
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
}
