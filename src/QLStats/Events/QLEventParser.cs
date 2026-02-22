using System.Text.Json;

namespace QLStats.Events;

public static class QLEventParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper
    };

    public static QlEnvelope? Parse(string rawJson)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("TYPE", out var typeProp))
            return null;

        if (!root.TryGetProperty("DATA", out var dataProp))
            return null;

        var dataJson = dataProp.GetRawText();

        return typeProp.GetString() switch
        {
            "MATCH_STARTED"  => new QlEnvelope<MatchStartedData>  { Data = JsonSerializer.Deserialize<MatchStartedData>(dataJson, Options) },
            "MATCH_REPORT"   => new QlEnvelope<MatchReportData>   { Data = JsonSerializer.Deserialize<MatchReportData>(dataJson, Options) },
            "PLAYER_KILL"    => new QlEnvelope<PlayerKillData>    { Data = JsonSerializer.Deserialize<PlayerKillData>(dataJson, Options) },
            "ROUND_OVER"     => new QlEnvelope<RoundOverData>     { Data = JsonSerializer.Deserialize<RoundOverData>(dataJson, Options) },
            "PLAYER_STATS"   => new QlEnvelope<PlayerStatsData>   { Data = JsonSerializer.Deserialize<PlayerStatsData>(dataJson, Options) },
            "PLAYER_CONNECT" => new QlEnvelope<PlayerConnectData> { Data = JsonSerializer.Deserialize<PlayerConnectData>(dataJson, Options) },
            "PLAYER_MEDAL"   => new QlEnvelope<PlayerMedalData>   { Data = JsonSerializer.Deserialize<PlayerMedalData>(dataJson, Options) },
            _ => null
        };
    }
}
