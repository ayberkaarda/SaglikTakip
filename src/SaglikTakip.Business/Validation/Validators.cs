using System.Globalization;
using FluentValidation;
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
