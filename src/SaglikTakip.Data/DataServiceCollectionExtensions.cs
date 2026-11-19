using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SaglikTakip.Data.Persistence;
using SaglikTakip.Data.Repositories;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Data;

public static class DataServiceCollectionExtensions
{
    /// <summary>Environment variable that overrides the configured connection string.</summary>
    public const string ConnectionEnvironmentVariable = "SAGLIKTAKIP_CONNECTION";

    /// <summary>Registers the data access layer on SQL Server.</summary>
    public static IServiceCollection AddSqlServerDataAccess(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddDataAccess(options => options.UseSqlServer(connectionString));
    }

    /// <summary>
    /// Registers the data access layer with a caller supplied provider configuration, so tests can
    /// plug in another provider.
    /// </summary>
    public static IServiceCollection AddDataAccess(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureProvider);

        services.AddDbContextFactory<HealthDbContext>(configureProvider);
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IHealthRecordRepository, HealthRecordRepository>();
        services.AddSingleton<IExerciseRepository, ExerciseRepository>();
        services.AddSingleton<IHealthGoalRepository, HealthGoalRepository>();
        services.AddSingleton<IReminderRepository, ReminderRepository>();
        return services;
    }
}
