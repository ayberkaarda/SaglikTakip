namespace SaglikTakip.Domain.Entities;

/// <summary>A person whose health records and exercises are tracked (table: Kullanicilar).</summary>
public sealed class User
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }

    /// <summary>One of <see cref="Genders.All"/>.</summary>
    public string Gender { get; set; } = string.Empty;

    /// <summary>Text shown in the user pickers, for example "Ayşe - 30 Yaş - Kadın".</summary>
    public string DisplayName => $"{Name} - {Age} Yaş - {Gender}";

    public override string ToString() => DisplayName;
}
