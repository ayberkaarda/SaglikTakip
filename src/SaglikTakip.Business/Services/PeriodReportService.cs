using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Reporting;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Business.Services;

public interface IPeriodReportService
{
    /// <summary>
    /// Builds the report of the user from <paramref name="firstDay"/> to <paramref name="lastDay"/>
    /// (whole days, both included), for example a week or a month.
    /// </summary>
    Task<PeriodReport> BuildAsync(int userId, DateTime firstDay, DateTime lastDay, CancellationToken cancellationToken = default);
}

internal sealed class PeriodReportService(
    IUserRepository users,
    IHealthRecordRepository records,
    IHealthGoalRepository goals,
    TimeProvider timeProvider) : IPeriodReportService
{
    public async Task<PeriodReport> BuildAsync(
        int userId,
        DateTime firstDay,
        DateTime lastDay,
        CancellationToken cancellationToken = default)
    {
        var range = RangeGuard.Days(firstDay, lastDay);
        await UserGuard.EnsureExistsAsync(users, userId, cancellationToken).ConfigureAwait(false);
        var user = await users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false)
            ?? throw new BusinessRuleException("Seçilen kullanıcı bulunamadı.");

        var inRange = await records.GetByUserBetweenAsync(userId, range.From, range.To, cancellationToken).ConfigureAwait(false);
        var goal = await goals.GetByUserAsync(userId, cancellationToken).ConfigureAwait(false);

        return new PeriodReport
        {
            User = user,
            From = range.From,
            To = range.To,
            GeneratedAt = timeProvider.GetLocalNow().DateTime,
            Records = inRange,
            Weight = TrendCalculator.Summarize(inRange, HealthMetric.Weight),
            Pulse = TrendCalculator.Summarize(inRange, HealthMetric.Pulse),
            Goal = goal is null ? null : GoalEvaluator.Evaluate(goal, inRange),
        };
    }
}
