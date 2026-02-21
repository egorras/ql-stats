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
- `dotnet build src/QLStats/QLStats.csproj` — build main project only
- `dotnet ef migrations add <Name> --project src/QLStats` — add EF migration
- `dotnet ef database update --project src/QLStats` — apply pending migrations manually

## Key Locations
- `src/QLStats/Services/` — background services (ZMQ, ingestion, replay, standings)
- `src/QLStats/Data/Entities/` — EF Core entity models
- `src/QLStats/Components/Pages/` — Blazor pages
- `src/QLStats/Mcp/QlStatsMcpTools.cs` — MCP tool definitions
- `src/QLStats.ServiceDefaults/` — shared OpenTelemetry, health checks, service discovery config

## Notes
- Connection string named `qlstats-db`; injected by Aspire in dev, set in appsettings for prod
- User secrets ID: `qlstats-app`; AppHost user secrets ID: `qlstats-apphost`
- `appsettings.Development.json` contains a working local DB connection string — no secrets needed for basic local dev
- `ZmqListenerService` is registered as both singleton and `IHostedService`; use `IZmqListenerControl` to trigger reload
- No test projects currently exist

## MCP Tools (8 available at `/mcp`)
`ListSeasons`, `GetSeasonStandings`, `ListPlayers`, `GetPlayerStats`, `GetMatchDetails`, `QueryMatches`, `GetHeadToHead`
