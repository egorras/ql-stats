namespace QLStats.Data.Entities;

public class MatchPlayer
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public int PlayerId { get; set; }
    public string Team { get; set; } = "";  // "RED" or "BLUE"
    public bool Won { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int DamageDealt { get; set; }
    public int DamageTaken { get; set; }
    public int RoundsWon { get; set; }
    public int RoundsLost { get; set; }

    public Match Match { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
