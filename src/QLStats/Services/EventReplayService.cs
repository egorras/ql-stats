using Microsoft.EntityFrameworkCore;
using QLStats.Data;

namespace QLStats.Services;

public class EventReplayService(
    IDbContextFactory<AppDbContext> dbFactory,
    MatchIngestionService ingestion,
    ILogger<EventReplayService> logger)
{
    public async Task<int> ReplayUnprocessedAsync()
    {
        logger.LogInformation("Starting replay of unprocessed events");
        await using var db = await dbFactory.CreateDbContextAsync();

        var events = await db.ZmqEvents
            .Where(e => !e.Processed)
            .Include(e => e.QLServer)
            .OrderBy(e => e.ReceivedAt)
            .ToListAsync();

        int count = 0;
        foreach (var ev in events)
        {
            await ProcessEventAsync(ev.Id, ev.QLServerId, ev.QLServer?.Name ?? "", ev.EventType, ev.RawJson, db);
            count++;
        }

        logger.LogInformation("Replayed {Count} unprocessed events", count);
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
        await db.GameSessions.ExecuteDeleteAsync();

        var query = db.ZmqEvents
            .Include(e => e.QLServer)
            .OrderBy(e => e.ReceivedAt)
            .AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(e => e.ReceivedAt >= fromDate.Value);

        var events = await query.ToListAsync();

        int count = 0;
        foreach (var ev in events)
        {
            await ProcessEventAsync(ev.Id, ev.QLServerId, ev.QLServer?.Name ?? "", ev.EventType, ev.RawJson, db);
            count++;
        }

        logger.LogInformation("Full replay complete: {Count} events processed", count);
        return count;
    }

    private async Task ProcessEventAsync(long eventId, int serverId, string serverName,
        string eventType, string rawJson, AppDbContext db)
    {
        try
        {
            var (_, data) = QLEventParser.ParseEnvelope(rawJson);

            switch (eventType.ToUpperInvariant())
            {
                case "MATCH_STARTED":
                {
                    var ev = QLEventParser.ParseMatchStarted(data);
                    if (ev is not null) await ingestion.HandleMatchStartedAsync(ev, serverId, serverName);
                    break;
                }
                case "MATCH_REPORT":
                {
                    var ev = QLEventParser.ParseMatchReport(data);
                    if (ev is not null) await ingestion.HandleMatchReportAsync(ev, serverId);
                    break;
                }
                case "PLAYER_KILL":
                {
                    var ev = QLEventParser.ParsePlayerKill(data);
                    if (ev is not null) await ingestion.HandlePlayerKillAsync(ev);
                    break;
                }
                case "ROUND_OVER":
                {
                    var ev = QLEventParser.ParseRoundOver(data);
                    if (ev is not null) await ingestion.HandleRoundOverAsync(ev);
                    break;
                }
            }

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
