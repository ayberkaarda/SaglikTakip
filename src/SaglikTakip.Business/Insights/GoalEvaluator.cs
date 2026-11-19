using System.Globalization;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Business.Insights;

/// <summary>Which side of a limit a measurement is on.</summary>
public enum LimitSide
{
    BelowMinimum,
    AboveMaximum,
}

/// <summary>A measurement outside a goal limit.</summary>
public sealed record ThresholdAlert(int RecordId, DateTime Date, HealthMetric Metric, double Value, double Limit, LimitSide Side);

/// <summary>A user's goal evaluated against a set of measurements.</summary>
public sealed class GoalStatus
{
    public required HealthGoal Goal { get; init; }

    /// <summary>Weight of the latest measurement considered, if any.</summary>
    public required double? CurrentWeight { get; init; }

    public required DateTime? CurrentWeightDate { get; init; }

    /// <summary>
    /// Progress from <see cref="HealthGoal.StartWeight"/> to <see cref="HealthGoal.TargetWeight"/>
    /// in percent (0-100), or <see langword="null"/> when start, target or a measurement is missing.
    /// </summary>
    public required double? ProgressPercent { get; init; }

    /// <summary>Measurements outside the goal limits, oldest first.</summary>
    public required IReadOnlyList<ThresholdAlert> Alerts { get; init; }
}

/// <summary>Pure goal rules. Limits are inclusive: a value equal to a limit is within range.</summary>
public static class GoalEvaluator
{
    /// <summary>Weight and pulse of <paramref name="record"/> compared with the goal's limits.</summary>
    public static IReadOnlyList<ThresholdAlert> Check(HealthGoal goal, HealthRecord record)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(record);

        var alerts = new List<ThresholdAlert>(2);
        Add(alerts, record, HealthMetric.Weight, record.Weight, goal.MinWeight, goal.MaxWeight);
        Add(alerts, record, HealthMetric.Pulse, record.Pulse, goal.MinPulse, goal.MaxPulse);
        return alerts;
    }

    /// <summary>
    /// Share of the way from the start weight to the target weight that <paramref name="currentWeight"/>
    /// has covered, clamped to 0-100. Works for losing and for gaining weight. When start and target are
    /// equal the goal is either reached (100) or not (0).
    /// </summary>
    public static double? Progress(HealthGoal goal, double? currentWeight)
    {
        ArgumentNullException.ThrowIfNull(goal);

        if (goal.TargetWeight is not { } target || goal.StartWeight is not { } start || currentWeight is not { } current)
        {
            return null;
        }

        if (start == target)
        {
            return current == target ? 100 : 0;
        }

        var percent = (start - current) / (start - target) * 100;
        return Math.Clamp(percent, 0, 100);
    }

    /// <summary>Evaluates every record (any order); the latest one (date, then entry) is the current weight.</summary>
    public static GoalStatus Evaluate(HealthGoal goal, IEnumerable<HealthRecord> records)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(records);

        var ordered = records.OrderBy(r => r.Date.Date).ThenBy(r => r.Id).ToList();
        var latest = ordered.Count > 0 ? ordered[^1] : null;

        return new GoalStatus
        {
            Goal = goal,
            CurrentWeight = latest?.Weight,
            CurrentWeightDate = latest?.Date.Date,
            ProgressPercent = Progress(goal, latest?.Weight),
            Alerts = ordered.SelectMany(r => Check(goal, r)).ToList(),
        };
    }

    /// <summary>Turkish description, for example "03.06.2025: Nabız 130 atım/dk, üst sınır 120".</summary>
    public static string Describe(ThresholdAlert alert, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(alert);
        ArgumentNullException.ThrowIfNull(culture);

        var (name, unit) = alert.Metric == HealthMetric.Weight ? ("Kilo", "kg") : ("Nabız", "atım/dk");
        var side = alert.Side == LimitSide.BelowMinimum ? "alt sınır" : "üst sınır";
        return string.Format(
            culture,
            "{0:d}: {1} {2:0.#} {3}, {4} {5:0.#}",
            alert.Date,
            name,
            alert.Value,
            unit,
            side,
            alert.Limit);
    }

    private static void Add(List<ThresholdAlert> alerts, HealthRecord record, HealthMetric metric, double value, double? min, double? max)
    {
        if (min is { } low && value < low)
        {
            alerts.Add(new ThresholdAlert(record.Id, record.Date.Date, metric, value, low, LimitSide.BelowMinimum));
        }
        else if (max is { } high && value > high)
        {
            alerts.Add(new ThresholdAlert(record.Id, record.Date.Date, metric, value, high, LimitSide.AboveMaximum));
        }
    }
}
