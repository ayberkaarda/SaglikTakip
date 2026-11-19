namespace SaglikTakip.Domain.Entities;

/// <summary>A dated weight and pulse measurement of a user (table: SaglikKayitlari).</summary>
public sealed class HealthRecord
{
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>Day of the measurement; the time part is not stored.</summary>
    public DateTime Date { get; set; }

    /// <summary>Body weight in kilograms.</summary>
    public double Weight { get; set; }

    /// <summary>Pulse in beats per minute.</summary>
    public int Pulse { get; set; }

    public string? Notes { get; set; }
}
