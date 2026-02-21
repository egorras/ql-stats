namespace QLStats.Data.Entities;

public class Season
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsActive { get; set; }

    // Scoring formula coefficients
    public decimal PointsPerKill { get; set; } = 1m;
    public decimal PointsPerDeath { get; set; } = -0.5m;
    public decimal PointsPerWin { get; set; } = 5m;
    public decimal PointsPerLoss { get; set; } = 0m;
    public decimal PointsPerRoundWon { get; set; } = 0.5m;
    public decimal PointsPerRoundLost { get; set; } = 0m;
    public decimal PointsPerDamageDealt { get; set; } = 0.01m;

    public ICollection<GameSession> GameSessions { get; set; } = [];
}
