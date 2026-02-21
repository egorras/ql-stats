namespace QLStats.Data.Entities;

public class ZmqEvent
{
    public long Id { get; set; }
    public int QLServerId { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string EventType { get; set; } = "";
    public string RawJson { get; set; } = "";
    public bool Processed { get; set; }

    public QLServer QLServer { get; set; } = null!;
}
