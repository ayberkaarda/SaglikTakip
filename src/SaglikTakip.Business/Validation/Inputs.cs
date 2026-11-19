namespace SaglikTakip.Business.Validation;

/// <summary>User data entered on the "Kullanıcı Ekle" screen. Text values are trimmed on creation.</summary>
internal sealed record UserInput(string Name, int Age, string Gender)
{
    public static UserInput Create(string? name, int age, string? gender) =>
        new(name?.Trim() ?? string.Empty, age, gender?.Trim() ?? string.Empty);
}

/// <summary>
/// A weight and pulse measurement. The date keeps only its day part and blank notes become
/// <see langword="null"/>.
/// </summary>
internal sealed record HealthRecordInput(int UserId, DateTime Date, decimal Weight, int Pulse, string? Notes)
{
    public static HealthRecordInput Create(int userId, DateTime date, decimal weight, int pulse, string? notes)
    {
        var trimmedNotes = notes?.Trim();
        return new(userId, date.Date, weight, pulse, string.IsNullOrEmpty(trimmedNotes) ? null : trimmedNotes);
    }
}

/// <summary>
/// Goal and alert limits entered on the "Hedefler" screen. Every value is optional;
/// <see langword="null"/> means "not set".
/// </summary>
public sealed record GoalInput(
    decimal? TargetWeight,
    decimal? StartWeight,
    decimal? MinWeight,
    decimal? MaxWeight,
    int? MinPulse,
    int? MaxPulse);

/// <summary>An exercise entry. The name is trimmed on creation.</summary>
internal sealed record ExerciseInput(int UserId, string Name, int DurationMinutes, int Repetitions)
{
    public static ExerciseInput Create(int userId, string? name, int durationMinutes, int repetitions) =>
        new(userId, name?.Trim() ?? string.Empty, durationMinutes, repetitions);
}
