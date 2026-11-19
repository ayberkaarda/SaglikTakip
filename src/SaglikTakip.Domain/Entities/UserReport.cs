namespace SaglikTakip.Domain.Entities;

/// <summary>Read model for the report screen: everything recorded for one user.</summary>
public sealed class UserReport
{
    public required User User { get; init; }

    /// <summary>Health records, newest first.</summary>
    public required IReadOnlyList<HealthRecord> HealthRecords { get; init; }

    /// <summary>Exercises, newest first.</summary>
    public required IReadOnlyList<Exercise> Exercises { get; init; }

    public HealthRecord? LatestHealthRecord => HealthRecords.Count > 0 ? HealthRecords[0] : null;

    public Exercise? LatestExercise => Exercises.Count > 0 ? Exercises[0] : null;
}
