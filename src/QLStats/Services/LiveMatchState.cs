namespace QLStats.Services;

public class LiveMatchDto
{
    public string MatchGuid { get; set; } = "";
    public string Map { get; set; } = "";
    public string GameType { get; set; } = "";
    public string ServerName { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public int RedScore { get; set; }
    public int BlueScore { get; set; }
    public int RoundNumber { get; set; }
    public List<LiveKillEntry> KillFeed { get; set; } = [];
    public List<LivePlayerState> Players { get; set; } = [];
}

public record LiveKillEntry(string KillerName, string VictimName, string Weapon, DateTime At);

public class LivePlayerState
{
    public string SteamId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Team { get; set; } = "";
    public int Kills { get; set; }
    public int Deaths { get; set; }
}

public class LiveMatchState
{
    private readonly Lock _lock = new();
    private LiveMatchDto? _current;

    public event Action? OnMatchStateChanged;

    public LiveMatchDto? Current
    {
        get { lock (_lock) { return _current; } }
    }

    public void StartMatch(string matchGuid, string map, string gameType, string serverName,
        IEnumerable<(string SteamId, string Name, string Team)>? players = null)
    {
        lock (_lock)
        {
            _current = new LiveMatchDto
            {
                MatchGuid = matchGuid,
                Map = map,
                GameType = gameType,
                ServerName = serverName,
                StartedAt = DateTime.UtcNow
            };

            if (players is not null)
            {
                foreach (var (steamId, name, team) in players)
                    _current.Players.Add(new LivePlayerState { SteamId = steamId, Name = name, Team = team });
            }
        }
        OnMatchStateChanged?.Invoke();
    }

    public void RecordKill(string killerSteamId, string killerName, string victimSteamId, string victimName, string weapon)
    {
        lock (_lock)
        {
            if (_current is null) return;

            var killer = EnsurePlayer(killerSteamId, killerName);
            var victim = EnsurePlayer(victimSteamId, victimName);
            killer.Kills++;
            victim.Deaths++;

            _current.KillFeed.Insert(0, new LiveKillEntry(killerName, victimName, weapon, DateTime.UtcNow));
            if (_current.KillFeed.Count > 50)
                _current.KillFeed.RemoveAt(_current.KillFeed.Count - 1);
        }
        OnMatchStateChanged?.Invoke();
    }

    public void RecordRoundOver(int winningTeam, int roundNumber)
    {
        lock (_lock)
        {
            if (_current is null) return;
            _current.RoundNumber = roundNumber;
            if (winningTeam == 1) _current.RedScore++;
            else if (winningTeam == 2) _current.BlueScore++;
        }
        OnMatchStateChanged?.Invoke();
    }

    public void UpdateMatchInfo(string map, string gameType, string serverName)
    {
        lock (_lock)
        {
            if (_current is null) return;
            if (!string.IsNullOrEmpty(map)) _current.Map = map;
            if (!string.IsNullOrEmpty(gameType)) _current.GameType = gameType;
            if (!string.IsNullOrEmpty(serverName)) _current.ServerName = serverName;
        }
        OnMatchStateChanged?.Invoke();
    }

    public void EndMatch()
    {
        lock (_lock) { _current = null; }
        OnMatchStateChanged?.Invoke();
    }

    private LivePlayerState EnsurePlayer(string steamId, string name)
    {
        var p = _current!.Players.FirstOrDefault(x => x.SteamId == steamId);
        if (p is null)
        {
            p = new LivePlayerState { SteamId = steamId, Name = name };
            _current.Players.Add(p);
        }
        return p;
    }
}
