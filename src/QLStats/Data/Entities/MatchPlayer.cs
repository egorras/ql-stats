using QLStats.Services;

namespace QLStats.Data.Entities;

public class MatchPlayer : ICanApplyEvent<PlayerStatsData>
{
    public int Id { get; set; }
    public int MatchId { get; set; }
    public int PlayerId { get; set; }
    public string Team { get; set; } = "";
    public bool Won { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int DamageDealt { get; set; }
    public int DamageTaken { get; set; }
    public int RoundsWon { get; set; }
    public int RoundsLost { get; set; }

    public Match Match { get; set; } = null!;
    public Player Player { get; set; } = null!;

    public void Apply(PlayerStatsData @event)
    {
        Team = @event.Team == 1 ? "RED" : @event.Team == 2 ? "BLUE" : "FREE";
        Won = @event.Win == 1;
        Kills = @event.Kills;
        Deaths = @event.Deaths;
        DamageDealt = @event.Damage?.Dealt ?? 0;
        DamageTaken = @event.Damage?.Taken ?? 0;
    }
}
