using System.Globalization;
using FluentValidation;
using SaglikTakip.Business.Insights;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Business.Validation;

/// <summary>User facing (Turkish) validation messages. They contain field names and limits only, never input values.</summary>
internal static class Messages
{
    public const string GenderRequired = "Lütfen cinsiyet seçin.";
    public const string InvalidDate = "Lütfen geçerli bir tarih seçin.";
    public const string FutureDate = "Sağlık kaydının tarihi ileri bir tarih olamaz.";
    public const string NoEffort = "Süre veya tekrar sayısından en az biri sıfırdan büyük olmalıdır.";
    public const string InvalidRange = "Başlangıç tarihi bitiş tarihinden sonra olamaz.";
    public const string EmptyGoal = "En az bir hedef veya sınır girin.";
    public const string WeightLimitsReversed = "En düşük kilo en yüksek kilodan büyük olamaz.";
    public const string PulseLimitsReversed = "En düşük nabız en yüksek nabızdan büyük olamaz.";
    public const string ReminderKindRequired = "Lütfen hatırlatıcı türünü seçin.";
    public const string ReminderInPast = "İlk hatırlatma zamanı geçmişte olamaz.";

    public static string Required(string fieldName) =>
        string.Format(CultureInfo.CurrentCulture, "{0} alanı boş bırakılamaz.", fieldName);

    public static string TooLong(string fieldName, int maxLength) =>
        string.Format(CultureInfo.CurrentCulture, "{0} en fazla {1} karakter olabilir.", fieldName, maxLength);

    public static string OutOfRange<T>(string fieldName, T minimum, T maximum) =>
        string.Format(CultureInfo.CurrentCulture, "{0} {1} ile {2} arasında olmalıdır.", fieldName, minimum, maximum);
}

internal static class ValidationRuleExtensions
{
    /// <summary>The value must be present (after trimming) and not longer than <paramref name="maxLength"/>.</summary>
    public static IRuleBuilderOptions<T, string> RequiredText<T>(
        this IRuleBuilder<T, string> rule, string fieldName, int maxLength) =>
        rule
            .NotEmpty().WithMessage(_ => Messages.Required(fieldName))
            .MaximumLength(maxLength).WithMessage(_ => Messages.TooLong(fieldName, maxLength));

    /// <summary><paramref name="minimum"/> &lt;= value &lt;= <paramref name="maximum"/>.</summary>
    public static IRuleBuilderOptions<T, TProperty> Between<T, TProperty>(
        this IRuleBuilder<T, TProperty> rule, string fieldName, TProperty minimum, TProperty maximum)
        where TProperty : IComparable<TProperty>, IComparable =>
        rule
            .InclusiveBetween(minimum, maximum)
            .WithMessage(_ => Messages.OutOfRange(fieldName, minimum, maximum));

    /// <summary>
    /// Validates <paramref name="input"/> and throws a <see cref="BusinessRuleException"/> carrying the
    /// first failure message, so the user sees one clear message at a time.
    /// </summary>
    public static void ValidateOrThrow<T>(this IValidator<T> validator, T input)
    {
        ArgumentNullException.ThrowIfNull(validator);

        var result = validator.Validate(input);
        if (!result.IsValid)
        {
            throw new BusinessRuleException(result.Errors[0].ErrorMessage);
        }
    }
}

internal sealed class UserInputValidator : AbstractValidator<UserInput>
{
    public const int MaxNameLength = 100;
    public const int MinAge = 0;
    public const int MaxAge = 150;

    public UserInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(u => u.Name).RequiredText("Ad", MaxNameLength);
        RuleFor(u => u.Age).Between("Yaş", MinAge, MaxAge);
        RuleFor(u => u.Gender)
            .Must(gender => Genders.All.Contains(gender, StringComparer.Ordinal))
            .WithMessage(Messages.GenderRequired);
    }
}

internal sealed class HealthRecordInputValidator : AbstractValidator<HealthRecordInput>
{
    public const decimal MinWeight = 1;
    public const decimal MaxWeight = 200;
    public const int MinPulse = 1;
    public const int MaxPulse = 250;
    public const int MaxNotesLength = 500;
    public static readonly DateTime MinDate = new(1900, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    public HealthRecordInputValidator(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Date.Date)
            .GreaterThanOrEqualTo(MinDate).WithMessage(Messages.InvalidDate)
            // "Today" is read on every validation, so a long running application keeps a correct limit.
            .Must(day => day <= timeProvider.GetLocalNow().Date).WithMessage(Messages.FutureDate);
        RuleFor(r => r.Weight).Between("Kilo", MinWeight, MaxWeight);
        RuleFor(r => r.Pulse).Between("Nabız", MinPulse, MaxPulse);
        RuleFor(r => r.Notes!)
            .MaximumLength(MaxNotesLength).WithMessage(_ => Messages.TooLong("Notlar", MaxNotesLength))
            .When(r => r.Notes is not null);
    }
}

/// <summary>Goal values use the same limits as measurements; minimums may equal maximums.</summary>
internal sealed class GoalInputValidator : AbstractValidator<GoalInput>
{
    public GoalInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(g => g)
            .Must(g => g.TargetWeight.HasValue || g.StartWeight.HasValue || g.MinWeight.HasValue
                || g.MaxWeight.HasValue || g.MinPulse.HasValue || g.MaxPulse.HasValue)
            .WithMessage(Messages.EmptyGoal);
        WeightRule(g => g.TargetWeight, "Hedef kilo");
        WeightRule(g => g.StartWeight, "Başlangıç kilosu");
        WeightRule(g => g.MinWeight, "En düşük kilo");
        WeightRule(g => g.MaxWeight, "En yüksek kilo");
        PulseRule(g => g.MinPulse, "En düşük nabız");
        PulseRule(g => g.MaxPulse, "En yüksek nabız");
        RuleFor(g => g)
            .Must(g => g.MinWeight is null || g.MaxWeight is null || g.MinWeight <= g.MaxWeight)
            .WithMessage(Messages.WeightLimitsReversed);
        RuleFor(g => g)
            .Must(g => g.MinPulse is null || g.MaxPulse is null || g.MinPulse <= g.MaxPulse)
            .WithMessage(Messages.PulseLimitsReversed);
    }

    private void WeightRule(System.Linq.Expressions.Expression<Func<GoalInput, decimal?>> property, string fieldName) =>
        RuleFor(property)
            .Must(value => value is null or (>= HealthRecordInputValidator.MinWeight and <= HealthRecordInputValidator.MaxWeight))
            .WithMessage(_ => Messages.OutOfRange(fieldName, HealthRecordInputValidator.MinWeight, HealthRecordInputValidator.MaxWeight));

    private void PulseRule(System.Linq.Expressions.Expression<Func<GoalInput, int?>> property, string fieldName) =>
        RuleFor(property)
            .Must(value => value is null or (>= HealthRecordInputValidator.MinPulse and <= HealthRecordInputValidator.MaxPulse))
            .WithMessage(_ => Messages.OutOfRange(fieldName, HealthRecordInputValidator.MinPulse, HealthRecordInputValidator.MaxPulse));
}

internal sealed class ReminderInputValidator : AbstractValidator<ReminderInput>
{
    public const int MaxTitleLength = 100;

    public ReminderInputValidator(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(r => r.Kind).Must(kind => Enum.IsDefined(kind)).WithMessage(Messages.ReminderKindRequired);
        RuleFor(r => r.Title).RequiredText("Başlık", MaxTitleLength);
        RuleFor(r => r.IntervalHours)
            .Between("Tekrar aralığı (saat)", ReminderSchedule.MinIntervalHours, ReminderSchedule.MaxIntervalHours);

        // The current minute is still accepted; "now" is read on every validation.
        RuleFor(r => r.FirstDueAt)
            .Must(first => first >= ReminderSchedule.ToMinute(timeProvider.GetLocalNow().DateTime))
            .WithMessage(Messages.ReminderInPast);
    }
}

internal sealed class ExerciseInputValidator : AbstractValidator<ExerciseInput>
{
    public const int MaxNameLength = 100;
    public const int MaxDurationMinutes = 1440;
    public const int MaxRepetitions = 1000;

    public ExerciseInputValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(e => e.Name).RequiredText("Egzersiz adı", MaxNameLength);
        RuleFor(e => e.DurationMinutes).Between("Süre (dakika)", 0, MaxDurationMinutes);
        RuleFor(e => e.Repetitions).Between("Tekrar sayısı", 0, MaxRepetitions);
        RuleFor(e => e)
            .Must(e => e.DurationMinutes > 0 || e.Repetitions > 0)
            .WithMessage(Messages.NoEffort);
    }
}
