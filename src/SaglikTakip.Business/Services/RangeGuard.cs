using SaglikTakip.Business.Validation;

namespace SaglikTakip.Business.Services;

/// <summary>Checks for the date ranges of the insight screens. Both ends are whole days, both included.</summary>
internal static class RangeGuard
{
    public static (DateTime From, DateTime To) Days(DateTime from, DateTime to)
    {
        if (from.Date > to.Date)
        {
            throw new BusinessRuleException(Messages.InvalidRange);
        }

        if (from.Date < HealthRecordInputValidator.MinDate)
        {
            throw new BusinessRuleException(Messages.InvalidDate);
        }

        return (from.Date, to.Date);
    }
}
