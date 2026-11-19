using SaglikTakip.Business.Insights;
using SaglikTakip.Business.Validation;
using SaglikTakip.Domain.Entities;
using SaglikTakip.Domain.Repositories;

namespace SaglikTakip.Business.Services;

public interface ITrendService
{
    /// <summary>
    /// Daily time series, summary and trailing moving average of <paramref name="metric"/> for the user,
    /// from <paramref name="firstDay"/> to <paramref name="lastDay"/> (whole days, both included).
    /// </summary>
    Task<MetricTrend> GetTrendAsync(
        int userId,
        HealthMetric metric,
        DateTime firstDay,
        DateTime lastDay,
        int movingAverageWindow,
        CancellationToken cancellationToken = default);
}

internal sealed class TrendService(IUserRepository users, IHealthRecordRepository records) : ITrendService
{
    public async Task<MetricTrend> GetTrendAsync(
        int userId,
        HealthMetric metric,
        DateTime firstDay,
        DateTime lastDay,
        int movingAverageWindow,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(metric))
        {
            throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown metric.");
        }

        if (movingAverageWindow is < TrendCalculator.MinWindow or > TrendCalculator.MaxWindow)
        {
            throw new BusinessRuleException(
                Messages.OutOfRange("Hareketli ortalama gün sayısı", TrendCalculator.MinWindow, TrendCalculator.MaxWindow));
        }

        var range = RangeGuard.Days(firstDay, lastDay);
        await UserGuard.EnsureExistsAsync(users, userId, cancellationToken).ConfigureAwait(false);

        var inRange = await records.GetByUserBetweenAsync(userId, range.From, range.To, cancellationToken).ConfigureAwait(false);

        return new MetricTrend
        {
            Metric = metric,
            From = range.From,
            To = range.To,
            MovingAverageWindow = movingAverageWindow,
            Points = TrendCalculator.DailyPoints(inRange, metric, movingAverageWindow),
            Summary = TrendCalculator.Summarize(inRange, metric),
        };
    }
}
