using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QLStats.Data;

/// <summary>
/// Design-time factory used by EF Core tooling (dotnet ef migrations add/update).
/// Uses a local dev connection string. The real connection string comes from Aspire at runtime.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=qlstats;Username=qlstats;Password=dev")
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AppDbContext(options);
    }
}
