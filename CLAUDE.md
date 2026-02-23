# QLStats

Quake Live statistics tracker. .NET 10 Blazor Server app orchestrated by .NET Aspire.

## Stack
- **UI**: Blazor Server + MudBlazor 9
- **DB**: PostgreSQL via EF Core 10 + Npgsql; migrations auto-apply on startup
- **Ingest**: NetMQ ZMQ subscriber (`ZmqListenerService`) — events: MATCH_STARTED, MATCH_REPORT, PLAYER_KILL, ROUND_OVER
- **MCP**: `ModelContextProtocol.AspNetCore` SSE server at `/mcp` (tools in `Mcp/QlStatsMcpTools.cs`)
- **Aspire**: Run via `src/QLStats.AppHost` — provisions Postgres + pgAdmin automatically

## Common Commands
- `dotnet run --project src/QLStats.AppHost` — start full stack (Aspire dashboard + app)
- `dotnet build qlstats.slnx` — build full solution
- `dotnet test qlstats.slnx` — run all tests
- `dotnet ef migrations add <Name> --project src/QLStats` — add EF migration
- `dotnet ef database update --project src/QLStats` — apply pending migrations manually

## Key Locations
- `src/QLStats/Services/` — background services (ZMQ, ingestion, replay, standings)
- `src/QLStats/Services/MapAnalyticsService.cs` — per-map stats (summaries, player breakdown, game-type breakdown)
- `src/QLStats/Services/DuoAnalyticsService.cs` — teammate pair win-rate analytics (in-memory LINQ self-join)
- `src/QLStats/Services/LegacyMigrationService.cs` — import from legacy PostgreSQL DB; config key: `LegacyDb:ConnectionString`
- `src/QLStats/Services/StandingsNotifier.cs` — event pub/sub for real-time standings refresh in Blazor
- `src/QLStats/Data/Entities/` — EF Core entity models
- `src/QLStats/Data/Entities/SeasonStanding.cs` — persisted standings snapshot (points, K/D, W/L) per player per season
- `src/QLStats/Components/Pages/` — Blazor pages
- `src/QLStats/Components/Pages/Maps.razor` — map analytics page
- `src/QLStats/Components/Pages/AdminMigrate.razor` — UI for triggering legacy migration + full data reset
- `src/QLStats/Mcp/QlStatsMcpTools.cs` — MCP tool definitions
- `src/QLStats.ServiceDefaults/` — shared OpenTelemetry, health checks, service discovery config
- `tests/QLStats.Tests/` — unit and integration tests

## Gotchas
- All `DateTime` values must be UTC; when reading from external/legacy DBs use `DateTime.SpecifyKind(dt, DateTimeKind.Utc)` before saving via Npgsql
- Full data reset FK-safe delete order: `SeasonStandings` → `ZmqEvents` → `RoundResults` → `MatchPlayers` → `Matches` → `Seasons` → `Players`
- EF cannot do same-table pair (duo) joins in SQL — use `.ToListAsync()` first, then LINQ in-memory
- Migrations were squashed 2026-02-23; `20260223160856_InitialCreate` is the baseline — old migration history deleted
- Services use C# 12 primary constructor injection: `class MyService(AppDbContext db)` — no field declarations needed

## CI/CD
- GitHub Action defined in `.github/workflows/dotnet.yml` builds and tests the solution on push/PR to `main`.

## MCP Tools (8 available at `/mcp`)
`ListSeasons`, `GetSeasonStandings`, `ListPlayers`, `GetPlayerStats`, `GetMatchDetails`, `QueryMatches`, `GetHeadToHead`
