using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SelfBet.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for <c>dotnet ef</c> when no startup connection string is available.
/// </summary>
public sealed class SelfBetDbContextFactory : IDesignTimeDbContextFactory<SelfBetDbContext>
{
    public SelfBetDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULTCONNECTION")
            ?? "Host=localhost;Database=selfbet;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<SelfBetDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new SelfBetDbContext(options);
    }
}
