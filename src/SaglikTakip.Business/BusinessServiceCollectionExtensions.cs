using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SaglikTakip.Business.Services;
using SaglikTakip.Business.Validation;

namespace SaglikTakip.Business;

public static class BusinessServiceCollectionExtensions
{
    /// <summary>Registers the business services. Repositories must be registered separately.</summary>
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IValidator<UserInput>, UserInputValidator>();
        services.AddSingleton<IValidator<HealthRecordInput>, HealthRecordInputValidator>();
        services.AddSingleton<IValidator<ExerciseInput>, ExerciseInputValidator>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IHealthRecordService, HealthRecordService>();
        services.AddSingleton<IExerciseService, ExerciseService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<ITrendService, TrendService>();
        services.AddSingleton<IValidator<GoalInput>, GoalInputValidator>();
        services.AddSingleton<IGoalService, GoalService>();
        services.AddSingleton<IValidator<ReminderInput>, ReminderInputValidator>();
        services.AddSingleton<IReminderService, ReminderService>();
        services.AddSingleton<IPeriodReportService, PeriodReportService>();
        return services;
    }
}
