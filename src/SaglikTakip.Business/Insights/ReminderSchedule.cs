namespace SaglikTakip.Business.Insights;

/// <summary>
/// Pure scheduling rules of periodic reminders. A reminder is due when its next time is at or before
/// "now"; marking it as done moves it forward by whole intervals to the first time strictly after "now",
/// so occurrences missed while the application was closed are counted, not replayed one by one.
/// </summary>
public static class ReminderSchedule
{
    public const int MinIntervalHours = 1;
    public const int MaxIntervalHours = 24 * 365;

    public static bool IsDue(DateTime nextDueAt, DateTime now) => nextDueAt <= now;

    /// <summary>How many occurrences have come due by <paramref name="now"/> (0 when not due yet).</summary>
    public static int DueOccurrences(DateTime nextDueAt, int intervalHours, DateTime now)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalHours, MinIntervalHours);

        if (!IsDue(nextDueAt, now))
        {
            return 0;
        }

        var passed = (now - nextDueAt).Ticks / TimeSpan.FromHours(intervalHours).Ticks;
        return (int)Math.Min(int.MaxValue, passed + 1);
    }

    /// <summary>The first occurrence strictly after <paramref name="now"/>; unchanged when not due yet.</summary>
    public static DateTime NextAfter(DateTime nextDueAt, int intervalHours, DateTime now)
    {
        var occurrences = DueOccurrences(nextDueAt, intervalHours, now);
        return nextDueAt.AddTicks(TimeSpan.FromHours(intervalHours).Ticks * occurrences);
    }

    /// <summary>Drops seconds and smaller parts; reminders are scheduled to the minute.</summary>
    public static DateTime ToMinute(DateTime value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMinute));
}
