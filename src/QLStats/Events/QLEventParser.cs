using System.Text.Json;
using System.Text.Json.Serialization;

namespace QLStats.Events;

// QL ZMQ stats send boolean-like fields (SUICIDE, TEAMKILL, BOT, ...) as integers 0/1
// rather than JSON true/false. This converter accepts both forms.
internal sealed class FlexBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.True    => true,
            JsonTokenType.False   => false,
            JsonTokenType.Number  => reader.GetInt32() != 0,
            _ => throw new JsonException($"Cannot convert token {reader.TokenType} to bool")
        };

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        => writer.WriteBooleanValue(value);
}

public static class QLEventParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper,
        Converters = { new FlexBoolConverter() }
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
