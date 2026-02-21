namespace QLStats.Data.Entities;

public class Player
{
    public int Id { get; set; }
    public string SteamId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }

    public ICollection<MatchPlayer> MatchPlayers { get; set; } = [];
}
