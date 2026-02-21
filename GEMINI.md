# Gemini Configuration

Follow all project standards, technical stack requirements, and workflow instructions defined in [CLAUDE.md](./CLAUDE.md).

## Technical Context
- **Primary Stack**: .NET 10, Blazor Server, MudBlazor 9, PostgreSQL (EF Core 10).
- **Architecture**: .NET Aspire orchestration.
- **Key Service**: `ZmqListenerService` (NetMQ) for real-time game event ingestion.

## Additional Gemini-Specific Instructions
- When adding features, ensure they align with the `Mcp/QlStatsMcpTools.cs` definitions if they should be exposed via the MCP server.
- Always use the provided Aspire dashboard for local development inspection.
