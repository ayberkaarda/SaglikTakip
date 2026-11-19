namespace SaglikTakip.Domain.Entities;

/// <summary>What a reminder is about.</summary>
public enum ReminderKind
{
    Measurement = 0,
    Exercise = 1,
}

/// <summary>
/// A periodic reminder of a user (table: Hatirlaticilar). <see cref="NextDueAt"/> is local time; when
/// a due reminder is marked as done it moves forward by whole intervals to the first time after "now".
/// </summary>
public sealed class Reminder
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public ReminderKind Kind { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Repeat interval in hours.</summary>
    public int IntervalHours { get; set; }

    /// <summary>Next time the reminder is due (local time).</summary>
    public DateTime NextDueAt { get; set; }
}
