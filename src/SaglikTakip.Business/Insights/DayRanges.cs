using SaglikTakip.Business.Validation;

namespace SaglikTakip.Business.Insights;

/// <summary>Common date ranges of the insight screens; both ends are whole days and both are included.</summary>
public static class DayRanges
{
    /// <summary>Earliest day a health record can have.</summary>
    public static DateTime Earliest => HealthRecordInputValidator.MinDate;

    /// <summary>The <paramref name="days"/> days that end with <paramref name="today"/> (7 = a week).</summary>
    public static (DateTime First, DateTime Last) LastDays(DateTime today, int days)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        return (today.Date.AddDays(1 - days), today.Date);
    }

    /// <summary>From the first day of <paramref name="today"/>'s month up to <paramref name="today"/>.</summary>
    public static (DateTime First, DateTime Last) MonthToDate(DateTime today) =>
        (new DateTime(today.Year, today.Month, 1, 0, 0, 0, today.Kind), today.Date);

    /// <summary>The whole calendar month before <paramref name="today"/>'s month.</summary>
    public static (DateTime First, DateTime Last) PreviousMonth(DateTime today)
    {
        var first = new DateTime(today.Year, today.Month, 1, 0, 0, 0, today.Kind).AddMonths(-1);
        return (first, first.AddMonths(1).AddDays(-1));
    }
}
