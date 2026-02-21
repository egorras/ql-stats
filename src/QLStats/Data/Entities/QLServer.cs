namespace QLStats.Data.Entities;

public class QLServer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ZmqAddress { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int ReconnectIntervalMs { get; set; } = 5000;

    public ICollection<Match> Matches { get; set; } = [];
    public ICollection<ZmqEvent> ZmqEvents { get; set; } = [];
}
