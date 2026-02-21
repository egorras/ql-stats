namespace QLStats.Data.Entities;

public class GameSession
{
    public int Id { get; set; }
    public DateOnly SessionDate { get; set; }
    public string? Notes { get; set; }
    public int? SeasonId { get; set; }

    public Season? Season { get; set; }
    public ICollection<Match> Matches { get; set; } = [];
}
