namespace QLStats.Data.Entities;

public class SeasonStanding
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public int PlayerId { get; set; }
    public decimal TotalPoints { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int RoundsWon { get; set; }
    public int RoundsLost { get; set; }
    public int DamageDealt { get; set; }
    public int MatchesPlayed { get; set; }
    public DateTime SnappedAt { get; set; }

    public Season Season { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
