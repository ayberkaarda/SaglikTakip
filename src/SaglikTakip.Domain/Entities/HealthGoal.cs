namespace SaglikTakip.Domain.Entities;

/// <summary>
/// Goal and alert limits of one user (table: Hedefler). Every value is optional; at most one goal
/// exists per user. Limits are inclusive: a measurement equal to a limit is within range.
/// </summary>
public sealed class HealthGoal
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Weight the user wants to reach, in kilograms.</summary>
    public double? TargetWeight { get; set; }

    /// <summary>Weight at the start of the goal; progress is measured from here to <see cref="TargetWeight"/>.</summary>
    public double? StartWeight { get; set; }

    public double? MinWeight { get; set; }

    public double? MaxWeight { get; set; }

    public int? MinPulse { get; set; }

    public int? MaxPulse { get; set; }
}
