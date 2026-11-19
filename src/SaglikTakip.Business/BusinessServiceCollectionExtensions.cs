using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SaglikTakip.Business.Services;

namespace SaglikTakip.Business;

public static class BusinessServiceCollectionExtensions
{
    /// <summary>Registers the business services. Repositories must be registered separately.</summary>
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IHealthRecordService, HealthRecordService>();
        services.AddSingleton<IExerciseService, ExerciseService>();
        services.AddSingleton<IReportService, ReportService>();
        return services;
    }
}
