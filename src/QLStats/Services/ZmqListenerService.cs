using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using NetMQ;
using NetMQ.Sockets;
using QLStats.Data;

namespace QLStats.Services;

public class ZmqListenerService(
    IDbContextFactory<AppDbContext> dbFactory,
    EventStoreService eventStore,
    MatchIngestionService ingestion,
    ILogger<ZmqListenerService> logger)
    : BackgroundService, IZmqListenerControl
{
    private readonly Dictionary<int, CancellationTokenSource> _serverTasks = [];
    private readonly ConcurrentDictionary<int, ZmqServerStatus> _statuses = [];
    private readonly SemaphoreSlim _reloadLock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ReloadAsync();

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }

        // Cancel all server loops on shutdown
        foreach (var cts in _serverTasks.Values)
            cts.Cancel();
    }

    public IEnumerable<ZmqServerStatus> GetStatuses() => _statuses.Values;

    public async Task ReloadAsync()
    {
        await _reloadLock.WaitAsync();
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var servers = await db.QLServers.ToListAsync();

            var activeIds = servers.Where(s => s.IsActive).Select(s => s.Id).ToHashSet();
            var runningIds = _serverTasks.Keys.ToHashSet();

            // Stop removed/disabled servers
            foreach (var id in runningIds.Except(activeIds))
            {
                if (_serverTasks.TryGetValue(id, out var cts))
                {
                    cts.Cancel();
                    _serverTasks.Remove(id);
                    _statuses.TryRemove(id, out _);
                    logger.LogInformation("Stopped ZMQ listener for server {ServerId}", id);
                }
            }

            // Start new active servers
            foreach (var server in servers.Where(s => s.IsActive && !runningIds.Contains(s.Id)))
            {
                var cts = new CancellationTokenSource();
                _serverTasks[server.Id] = cts;
                _statuses[server.Id] = new ZmqServerStatus(server.Id, server.ZmqAddress, true, null, null);

                var capturedServer = server;
                _ = Task.Run(() => RunServerLoopAsync(capturedServer.Id,
                    capturedServer.ZmqAddress, capturedServer.ReconnectIntervalMs, cts.Token));
                logger.LogInformation("Started ZMQ listener for server {ServerId} at {Address}",
                    server.Id, server.ZmqAddress);
            }
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private async Task RunServerLoopAsync(int serverId, string address,
        int reconnectMs, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var socket = new SubscriberSocket();
                socket.Connect(address);
                socket.SubscribeToAnyTopic();
                logger.LogInformation("ZMQ connected: {Address}", address);

                // Update status to clear any previous error
                UpdateStatus(serverId, lastError: null);

                while (!ct.IsCancellationRequested)
                {
                    if (socket.TryReceiveFrameString(TimeSpan.FromSeconds(1), out var topic) &&
                        socket.TryReceiveFrameString(TimeSpan.FromMilliseconds(500), out var payload))
                    {
                        UpdateStatus(serverId, lastMessageAt: DateTime.UtcNow);
                        _ = ProcessMessageAsync(serverId, topic!, payload!);
                    }
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "ZMQ error for server {ServerId}, reconnecting in {Ms}ms", serverId, reconnectMs);
                UpdateStatus(serverId, lastError: ex.Message);
                try { await Task.Delay(reconnectMs, ct); } catch (OperationCanceledException) { break; }
            }
        }
    }

    private void UpdateStatus(int serverId, DateTime? lastMessageAt = null, string? lastError = null)
    {
        if (_statuses.TryGetValue(serverId, out var status))
        {
            _statuses[serverId] = status with
            {
                LastMessageAt = lastMessageAt ?? status.LastMessageAt,
                LastError = lastError ?? (lastMessageAt.HasValue ? null : status.LastError)
            };
        }
    }

    private async Task ProcessMessageAsync(int serverId, string topic, string payload)
    {
        try
        {
            var @event = QLEventParser.Parse(payload)?.GetEvent();

            // Warmup events update server last-seen (tracked before this call) but are not stored
            if (@event?.Warmup == true) return;

            var eventId = await eventStore.StoreAsync(serverId, payload);
            if (@event is not null)
                await ingestion.HandleAsync(@event, serverId);
            await eventStore.MarkProcessedAsync(eventId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing ZMQ message type '{Topic}' from server {ServerId}", topic, serverId);
        }
    }
}
