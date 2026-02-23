namespace QLStats.Services;

public class StandingsNotifier
{
    public event Action? StandingsChanged;

    public void NotifyStandingsChanged() => StandingsChanged?.Invoke();
}
