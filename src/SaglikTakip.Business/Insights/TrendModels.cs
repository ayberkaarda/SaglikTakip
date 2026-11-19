using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Business.Insights;

/// <summary>
/// Minimum, maximum and average of every single measurement in a range, plus the first and last
/// measured values (chronological order).
/// </summary>
public sealed record MetricSummary(int Count, double Minimum, double Maximum, double Average, double First, double Last);

/// <summary>
/// One day of a trend. Several measurements on the same day are combined into their mean;
/// <see cref="MeasurementCount"/> tells how many there were. <see cref="MovingAverage"/> is the
/// trailing mean of the last N daily values and is <see langword="null"/> until N days are available.
/// </summary>
public sealed record TrendPoint(DateTime Date, double Value, int MeasurementCount, double? MovingAverage);

/// <summary>Time series and summary of one metric of one user in a date range.</summary>
public sealed class MetricTrend
{
    public required HealthMetric Metric { get; init; }

    public required DateTime From { get; init; }

    public required DateTime To { get; init; }

    public required int MovingAverageWindow { get; init; }

    /// <summary>Daily points, oldest first.</summary>
    public required IReadOnlyList<TrendPoint> Points { get; init; }

    /// <summary><see langword="null"/> when the range has no measurements.</summary>
    public required MetricSummary? Summary { get; init; }
}
