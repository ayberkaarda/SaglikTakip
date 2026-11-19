namespace SaglikTakip.Domain.Entities;

/// <summary>A measured value of a <see cref="HealthRecord"/> that can be charted and checked against goals.</summary>
public enum HealthMetric
{
    /// <summary>Body weight in kilograms.</summary>
    Weight = 0,

    /// <summary>Pulse in beats per minute.</summary>
    Pulse = 1,
}
