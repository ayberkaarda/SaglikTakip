using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SaglikTakip.Data.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c> only. Reads the connection string from SAGLIKTAKIP_CONNECTION and
/// falls back to a local SQL Server Express instance.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HealthDbContext>
{
    private const string FallbackConnectionString =
        "Server=localhost\\SQLEXPRESS;Database=SaglikTakip;Trusted_Connection=True;TrustServerCertificate=True;";

    public HealthDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(DataServiceCollectionExtensions.ConnectionEnvironmentVariable);
        var options = new DbContextOptionsBuilder<HealthDbContext>()
            .UseSqlServer(string.IsNullOrWhiteSpace(connectionString) ? FallbackConnectionString : connectionString)
            .Options;
        return new HealthDbContext(options);
    }
}
