namespace QLStats.Data.Entities;

public class RoundResult
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public int RoundNumber { get; set; }
    public string TeamWon { get; set; } = "";  // "RED" or "BLUE"

    public Match Match { get; set; } = null!;
}
