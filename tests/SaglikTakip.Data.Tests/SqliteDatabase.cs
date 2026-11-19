using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaglikTakip.Business;
using SaglikTakip.Data.Persistence;

namespace SaglikTakip.Data.Tests;

/// <summary>
/// A private SQLite in-memory database wired through the application's own registrations.
/// The connection stays open for the lifetime of the instance, which keeps the database alive.
/// The schema and sample data come from the EF Core model (3 users, 4 health records, 3 exercises).
/// </summary>
public sealed class SqliteDatabase : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private ServiceProvider? _provider;

    public IServiceProvider Services => _provider ?? throw new InvalidOperationException("Not initialized.");

    public T Get<T>()
        where T : notnull => Services.GetRequiredService<T>();

    public HealthDbContext CreateContext() =>
        Services.GetRequiredService<IDbContextFactory<HealthDbContext>>().CreateDbContext();

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDataAccess(options => options.UseSqlite(_connection));
        services.AddBusinessServices();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        await Get<IDatabaseInitializer>().InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        await _connection.DisposeAsync();
    }
}
