namespace QLStats.Services;

public record ZmqServerStatus(int ServerId, string Name, bool IsRunning, DateTime? LastMessageAt, string? LastError);

public interface IZmqListenerControl
{
    Task ReloadAsync();
    IEnumerable<ZmqServerStatus> GetStatuses();
}
