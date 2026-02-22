using System.Text.Json;

namespace QLStats.Events;

public static class QLEventParser
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper
    };

    public static QlEnvelope? Parse(string rawJson) =>
        JsonSerializer.Deserialize<QlEnvelope>(rawJson, JsonSerializerOptions);
}
