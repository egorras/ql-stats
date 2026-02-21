namespace QLStats.Services;

public record ZmqServerStatus(int ServerId, string Address, bool IsRunning, DateTime? LastMessageAt, string? LastError);

public interface IZmqListenerControl
{
    Task ReloadAsync();
    IEnumerable<ZmqServerStatus> GetStatuses();
}
