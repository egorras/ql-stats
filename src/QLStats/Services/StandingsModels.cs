namespace QLStats.Services;

/// <summary>Live in-match projection used by the standings table.</summary>
public record LiveProjection(
    string Name,
    string Team,
    int Kills,
    int Deaths,
    int RoundsWon,
    int RoundsLost,
    decimal EstimatedPoints,
    Dictionary<string, decimal> Breakdown);

/// <summary>Transient point-change flash shown next to a player's live score.</summary>
public record PointDelta(decimal Delta, DateTime At);
