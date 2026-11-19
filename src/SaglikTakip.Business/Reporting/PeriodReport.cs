using SaglikTakip.Business.Insights;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Business.Reporting;

/// <summary>Summary of one user's measurements in a date range (both days included).</summary>
public sealed class PeriodReport
{
    public required User User { get; init; }

    public required DateTime From { get; init; }

    public required DateTime To { get; init; }

    public required DateTime GeneratedAt { get; init; }

    /// <summary>Measurements in the range, oldest first.</summary>
    public required IReadOnlyList<HealthRecord> Records { get; init; }

    public required MetricSummary? Weight { get; init; }

    public required MetricSummary? Pulse { get; init; }

    /// <summary>The user's goal evaluated on this range, or <see langword="null"/> when no goal is set.</summary>
    public required GoalStatus? Goal { get; init; }

    public int Days => (To - From).Days + 1;
}
