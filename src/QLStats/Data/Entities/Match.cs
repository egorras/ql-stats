using QLStats.Events;
using QLStats.Services;

namespace QLStats.Data.Entities;

public class Match :
    ICanApplyEvent<MatchStartedData>,
    ICanApplyEvent<MatchReportData>
{
    public int Id { get; set; }
    public int QLServerId { get; set; }
    public string MatchGuid { get; set; } = "";
    public string Map { get; set; } = "";
    public string GameType { get; set; } = "";
    public string ServerTitle { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int? TeamRedRounds { get; set; }
    public int? TeamBlueRounds { get; set; }

    public QLServer QLServer { get; set; } = null!;
    public ICollection<MatchPlayer> MatchPlayers { get; set; } = [];
    public ICollection<RoundResult> RoundResults { get; set; } = [];

    public void Apply(MatchStartedData @event)
    {
        MatchGuid = @event.MatchGuid.ToString();
        Map = @event.Map;
        GameType = @event.GameType;
        ServerTitle = @event.ServerTitle;
        StartedAt = DateTime.UtcNow;
    }

    public void Apply(MatchReportData @event)
    {
        // Only overwrite if MATCH_STARTED didn't already populate these
        if (string.IsNullOrEmpty(Map)) Map = @event.Map;
        if (string.IsNullOrEmpty(GameType)) GameType = @event.GameType;
        if (string.IsNullOrEmpty(ServerTitle)) ServerTitle = @event.ServerTitle;
        TeamRedRounds = @event.Tscore0;
        TeamBlueRounds = @event.Tscore1;
        FinishedAt = DateTime.UtcNow;
    }
}
