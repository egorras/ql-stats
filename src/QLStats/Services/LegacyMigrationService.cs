using Microsoft.EntityFrameworkCore;
using Npgsql;
using QLStats.Data;
using QLStats.Data.Entities;

namespace QLStats.Services;

public record MigrationResult(int SeasonsImported, int EventsImported, int SeasonsSkipped);

public class LegacyMigrationService(
    IDbContextFactory<AppDbContext> dbFactory,
    IConfiguration configuration,
    ILogger<LegacyMigrationService> logger)
{
    public async Task<MigrationResult> MigrateAsync()
    {
        var legacyConnStr = configuration["LegacyDb:ConnectionString"]
            ?? throw new InvalidOperationException("LegacyDb:ConnectionString is not configured.");

        await using var legacyConn = new NpgsqlConnection(legacyConnStr);
        await legacyConn.OpenAsync();

        await using var db = await dbFactory.CreateDbContextAsync();

        // Find or create the legacy QLServer entry
        var legacyServer = await db.QLServers
            .FirstOrDefaultAsync(s => s.ZmqAddress == "legacy://quake-stats");

        if (legacyServer is null)
        {
            legacyServer = new QLServer { ZmqAddress = "legacy://quake-stats", IsActive = false };
            db.QLServers.Add(legacyServer);
            await db.SaveChangesAsync();
        }

        // Idempotent: delete previously migrated events before re-importing
        await db.ZmqEvents
            .Where(e => e.QLServerId == legacyServer.Id)
            .ExecuteDeleteAsync();

        // --- Import seasons ---
        var seasonsImported = 0;
        var seasonsSkipped = 0;
        bool anyActiveInNewDb = await db.Seasons.AnyAsync(s => s.IsActive);

        await using (var cmd = legacyConn.CreateCommand())
        {
            cmd.CommandText = "SELECT name, started_at, ended_at, is_active FROM seasons ORDER BY started_at";
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                var startedAt = reader.GetDateTime(1);
                DateTime? endedAt = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
                var isActive = reader.GetBoolean(3);

                if (await db.Seasons.AnyAsync(s => s.Name == name))
                {
                    seasonsSkipped++;
                    continue;
                }

                // Avoid violating the one-active-season unique constraint
                bool insertAsActive = isActive && !anyActiveInNewDb;

                db.Seasons.Add(new Season
                {
                    Name = name,
                    StartDate = DateOnly.FromDateTime(startedAt),
                    EndDate = endedAt.HasValue ? DateOnly.FromDateTime(endedAt.Value) : null,
                    IsActive = insertAsActive,
                    Rules = DefaultRules()
                });
                seasonsImported++;
            }
        }

        await db.SaveChangesAsync();

        // --- Import events in batches of 500 ---
        var eventsImported = 0;
        var batch = new List<ZmqEvent>(500);

        await using (var cmd = legacyConn.CreateCommand())
        {
            cmd.CommandText = "SELECT created_at, event_type, event_data FROM match_events ORDER BY created_at";
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var createdAt = reader.GetDateTime(0);
                var eventType = reader.GetString(1);
                var eventData = reader.GetString(2);

                // Reconstruct full ZMQ envelope from stripped event_data
                var rawJson = $$$"""{"TYPE":"{{{eventType}}}","DATA":{{{eventData}}}}""";

                batch.Add(new ZmqEvent
                {
                    RawJson = rawJson,
                    ReceivedAt = createdAt.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)
                        : createdAt.ToUniversalTime(),
                    EventType = eventType,
                    QLServerId = legacyServer.Id,
                    Processed = false
                });

                eventsImported++;

                if (batch.Count >= 500)
                {
                    db.ZmqEvents.AddRange(batch);
                    await db.SaveChangesAsync();
                    batch.Clear();
                }
            }
        }

        if (batch.Count > 0)
        {
            db.ZmqEvents.AddRange(batch);
            await db.SaveChangesAsync();
        }

        logger.LogInformation(
            "Legacy migration complete: {SeasonsImported} seasons imported, {SeasonsSkipped} skipped, {EventsImported} events imported",
            seasonsImported, seasonsSkipped, eventsImported);

        return new MigrationResult(seasonsImported, eventsImported, seasonsSkipped);
    }

    // Matches the legacy scoring formula exactly:
    //   kills*1 + floor(damage/150) + all_medals*1 + rounds_won*2 + win?8 - falls*1
    private static List<ScoringRule> DefaultRules() =>
    [
        new() { Type = ScoringRuleType.KillsMultiplier,     Value = 1m,          SortOrder = 0 },
        new() { Type = ScoringRuleType.SuicidesMultiplier,  Value = -1m,         SortOrder = 1 },
        new() { Type = ScoringRuleType.WinMultiplier,       Value = 8m,          SortOrder = 2 },
        new() { Type = ScoringRuleType.RoundsWonMultiplier, Value = 2m,          SortOrder = 3 },
        new() { Type = ScoringRuleType.DamageMultiplier,    Value = 1m / 150m,   SortOrder = 4 },
        new() { Type = ScoringRuleType.MedalMultiplier,     Value = 1m,          SortOrder = 5, MedalType = null },
    ];

    public async Task ResetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        // Delete in FK-safe order. Matches cascade to MatchPlayers + RoundResults.
        // Seasons cascade to ScoringRules. QLServer rows with Restrict FK must come last.
        await db.SeasonStandings.ExecuteDeleteAsync();
        await db.ZmqEvents.ExecuteDeleteAsync();
        await db.RoundResults.ExecuteDeleteAsync();
        await db.MatchPlayers.ExecuteDeleteAsync();
        await db.Matches.ExecuteDeleteAsync();
        await db.Seasons.ExecuteDeleteAsync();
        await db.Players.ExecuteDeleteAsync();

        // Remove the legacy server entry so MigrateAsync recreates it cleanly.
        // Live ZMQ servers (real addresses) are intentionally preserved.
        await db.QLServers
            .Where(s => s.ZmqAddress == "legacy://quake-stats")
            .ExecuteDeleteAsync();

        logger.LogInformation("Full data reset complete");
    }
}
