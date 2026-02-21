using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QLStats.Data;
using QLStats.Data.Entities;

namespace QLStats.Services;

public class EventStoreService(IDbContextFactory<AppDbContext> dbFactory, ILogger<EventStoreService> logger)
{
    public async Task<long> StoreAsync(int serverId, string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var eventType = doc.RootElement.TryGetProperty("TYPE", out var t) ? t.GetString() ?? "" : "";

            await using var db = await dbFactory.CreateDbContextAsync();
            var ev = new ZmqEvent
            {
                QLServerId = serverId,
                ReceivedAt = DateTime.UtcNow,
                EventType = eventType,
                RawJson = rawJson,
                Processed = false
            };
            db.ZmqEvents.Add(ev);
            await db.SaveChangesAsync();
            return ev.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to store ZmqEvent for server {ServerId}", serverId);
            return -1;
        }
    }

    public async Task MarkProcessedAsync(long eventId)
    {
        if (eventId <= 0) return;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            await db.ZmqEvents
                .Where(e => e.Id == eventId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Processed, true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to mark ZmqEvent {EventId} as processed", eventId);
        }
    }
}
