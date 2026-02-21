using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using QLStats.Components;
using QLStats.Data;
using QLStats.Mcp;
using QLStats.Services;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Database — reads "qlstats-db" connection string from Aspire env injection or appsettings
var connStr = builder.Configuration.GetConnectionString("qlstats-db")
    ?? throw new InvalidOperationException("Connection string 'qlstats-db' not found.");

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(connStr));

// Scoped AppDbContext for Blazor pages/scoped services — created via the factory to avoid
// the root-scope conflict that arises when AddDbContext and AddDbContextFactory coexist.
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

// Application services
builder.Services.AddSingleton<LiveMatchState>();
builder.Services.AddSingleton<EventStoreService>();
builder.Services.AddSingleton<MatchIngestionService>();
builder.Services.AddScoped<StandingsService>();
builder.Services.AddScoped<EventReplayService>();

// ZMQ listener — registered as singleton + IHostedService + IZmqListenerControl
builder.Services.AddSingleton<ZmqListenerService>();
builder.Services.AddSingleton<IZmqListenerControl>(sp => sp.GetRequiredService<ZmqListenerService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ZmqListenerService>());

// Blazor Server
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();

// MCP Server — SSE transport, tools discovered from this assembly
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly(typeof(QlStatsMcpTools).Assembly);

var app = builder.Build();

// Auto-apply EF Core migrations on startup
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapDefaultEndpoints();   // Aspire health/readiness endpoints
app.MapMcp("/mcp");          // MCP SSE endpoint for Claude Desktop / Claude Code
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
