using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Business.Insights;

/// <summary>Pure trend calculations; no I/O, so every rule can be tested directly.</summary>
public static class TrendCalculator
{
    public const int MinWindow = 1;
    public const int MaxWindow = 90;

    /// <summary>Value of <paramref name="metric"/> in <paramref name="record"/>.</summary>
    public static double ValueOf(HealthRecord record, HealthMetric metric)
    {
        ArgumentNullException.ThrowIfNull(record);

        return metric switch
        {
            HealthMetric.Weight => record.Weight,
            HealthMetric.Pulse => record.Pulse,
            _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, "Unknown metric."),
        };
    }

    /// <summary>
    /// Trailing simple moving average. Element i is the mean of values[i - window + 1 .. i]; the first
    /// <paramref name="window"/> - 1 elements are <see langword="null"/> because the window is not full yet.
    /// A window of 1 returns the values themselves; a window larger than the input returns only nulls.
    /// </summary>
    public static IReadOnlyList<double?> MovingAverage(IReadOnlyList<double> values, int window)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentOutOfRangeException.ThrowIfLessThan(window, MinWindow);

        var result = new double?[values.Count];
        for (var i = window - 1; i < values.Count; i++)
        {
            // Summed per window instead of a running total, so no rounding drift builds up.
            var sum = 0d;
            for (var j = i - window + 1; j <= i; j++)
            {
                sum += values[j];
            }

            result[i] = sum / window;
        }

        return result;
    }

    /// <summary>Groups the records by day (mean per day) and adds the moving average; oldest day first.</summary>
    public static IReadOnlyList<TrendPoint> DailyPoints(IEnumerable<HealthRecord> records, HealthMetric metric, int window)
    {
        ArgumentNullException.ThrowIfNull(records);

        var days = records
            .GroupBy(r => r.Date.Date)
            .OrderBy(g => g.Key)
            .Select(g => (Day: g.Key, Mean: g.Average(r => ValueOf(r, metric)), Count: g.Count()))
            .ToList();

        var averages = MovingAverage(days.Select(d => d.Mean).ToList(), window);
        return days.Select((d, i) => new TrendPoint(d.Day, d.Mean, d.Count, averages[i])).ToList();
    }

    /// <summary>
    /// Summary over every single measurement (not the daily means), or <see langword="null"/> when there
    /// is none. First and last follow the date, then the entry order.
    /// </summary>
    public static MetricSummary? Summarize(IEnumerable<HealthRecord> records, HealthMetric metric)
    {
        ArgumentNullException.ThrowIfNull(records);

        var values = records
            .OrderBy(r => r.Date.Date)
            .ThenBy(r => r.Id)
            .Select(r => ValueOf(r, metric))
            .ToList();

        return values.Count == 0
            ? null
            : new MetricSummary(values.Count, values.Min(), values.Max(), values.Average(), values[0], values[^1]);
    }
}
