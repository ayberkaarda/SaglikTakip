namespace SaglikTakip.Domain.Entities;

/// <summary>Gender values accepted by the application. They are stored as text (Turkish).</summary>
public static class Genders
{
    public const string Male = "Erkek";
    public const string Female = "Kadın";
    public const string Other = "Diğer";

    public static IReadOnlyList<string> All { get; } = [Male, Female, Other];
}
