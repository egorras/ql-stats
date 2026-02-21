namespace QLStats.Data.Entities;

public class Match
{
    public int Id { get; set; }
    public int GameSessionId { get; set; }
    public int QLServerId { get; set; }
    public string MatchGuid { get; set; } = "";
    public string Map { get; set; } = "";
    public string GameType { get; set; } = "";
    public string ServerTitle { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int? TeamRedRounds { get; set; }
    public int? TeamBlueRounds { get; set; }

    public GameSession GameSession { get; set; } = null!;
    public QLServer QLServer { get; set; } = null!;
    public ICollection<MatchPlayer> MatchPlayers { get; set; } = [];
    public ICollection<RoundResult> RoundResults { get; set; } = [];
}
