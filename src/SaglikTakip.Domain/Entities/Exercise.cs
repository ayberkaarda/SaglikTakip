namespace SaglikTakip.Domain.Entities;

/// <summary>An exercise performed by a user (table: Egzersizler).</summary>
public sealed class Exercise
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public int Repetitions { get; set; }
}
