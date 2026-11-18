namespace SaglikTakip.Data.Persistence;

/// <summary>
/// Column sizes and value ranges enforced by the database. The business layer applies the same
/// or stricter rules; the legacy upgrade checks existing rows against these values.
/// </summary>
internal static class SchemaLimits
{
    public const int UserNameLength = 100;
    public const int GenderLength = 10;
    public const int NotesLength = 500;
    public const int ExerciseNameLength = 100;

    public const string AgeCheck = "[Yas] >= 0 AND [Yas] <= 150";
    public const string GenderCheck = "[Cinsiyet] IN (N'Erkek', N'Kadın', N'Diğer')";
    public const string WeightCheck = "[Kilo] >= 0 AND [Kilo] <= 200";
    public const string PulseCheck = "[Nabiz] >= 0 AND [Nabiz] <= 250";
    public const string DurationCheck = "[SureDakika] >= 0 AND [SureDakika] <= 1440";
    public const string RepetitionsCheck = "[TekrarSayisi] >= 0 AND [TekrarSayisi] <= 1000";
}
