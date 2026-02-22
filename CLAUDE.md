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
- `src/QLStats/Data/Entities/` — EF Core entity models
- `src/QLStats/Components/Pages/` — Blazor pages
- `src/QLStats/Mcp/QlStatsMcpTools.cs` — MCP tool definitions
- `src/QLStats.ServiceDefaults/` — shared OpenTelemetry, health checks, service discovery config
- `tests/QLStats.Tests/` — unit and integration tests

## CI/CD
- GitHub Action defined in `.github/workflows/dotnet.yml` builds and tests the solution on push/PR to `main`.

## MCP Tools (8 available at `/mcp`)
`ListSeasons`, `GetSeasonStandings`, `ListPlayers`, `GetPlayerStats`, `GetMatchDetails`, `QueryMatches`, `GetHeadToHead`
