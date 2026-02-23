using Microsoft.EntityFrameworkCore;
using QLStats.Data;
using QLStats.Events;

namespace QLStats.Services;

public class EventReplayService(
    IDbContextFactory<AppDbContext> dbFactory,
    MatchIngestionService ingestion,
    StandingsService standings,
    ILogger<EventReplayService> logger)
{
    public async Task<int> ReplayUnprocessedAsync()
    {
        logger.LogInformation("Starting replay of unprocessed events");
        await using var db = await dbFactory.CreateDbContextAsync();

        var events = await db.ZmqEvents
            .Where(e => !e.Processed)
            .OrderBy(e => e.ReceivedAt)
            .ToListAsync();

        int count = 0;
        foreach (var ev in events)
        {
            await ProcessEventAsync(ev.Id, ev.QLServerId, ev.RawJson, ev.ReceivedAt, db);
            count++;
        }

        logger.LogInformation("Replayed {Count} unprocessed events", count);
        await standings.SnapshotAllSeasonsAsync();
        return count;
    }

    public async Task<int> ReplayAllAsync(DateTime? fromDate = null)
    {
        logger.LogInformation("Starting full event replay from {From}", fromDate?.ToString("u") ?? "beginning");

        await using var db = await dbFactory.CreateDbContextAsync();

        // Truncate read model (order matters due to FK constraints)
        await db.RoundResults.ExecuteDeleteAsync();
        await db.MatchPlayers.ExecuteDeleteAsync();
        await db.Matches.ExecuteDeleteAsync();

        var query = db.ZmqEvents
            .OrderBy(e => e.ReceivedAt)
            .AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(e => e.ReceivedAt >= fromDate.Value);

        var events = await query.ToListAsync();

        int count = 0;
        foreach (var ev in events)
        {
            await ProcessEventAsync(ev.Id, ev.QLServerId, ev.RawJson, ev.ReceivedAt, db);
            count++;
        }

        logger.LogInformation("Full replay complete: {Count} events processed", count);
        await standings.SnapshotAllSeasonsAsync();
        return count;
    }

    private async Task ProcessEventAsync(long eventId, int serverId,
        string rawJson, DateTime eventTime, AppDbContext db)
    {
        try
        {
            var @event = QLEventParser.Parse(rawJson)?.GetEvent();
            if (@event is { Warmup: false })
                await ingestion.HandleAsync(@event, serverId, eventTime);

            await db.ZmqEvents
                .Where(e => e.Id == eventId)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.Processed, true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to replay event {EventId}", eventId);
        }
    }
}
