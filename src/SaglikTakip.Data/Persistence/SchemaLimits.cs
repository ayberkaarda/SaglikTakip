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

    /// <summary>Same rule without the SQL Server N'' prefix, for other providers (tests use SQLite).</summary>
    public const string PortableGenderCheck = "[Cinsiyet] IN ('Erkek', 'Kadın', 'Diğer')";
    public const string WeightCheck = "[Kilo] >= 0 AND [Kilo] <= 200";
    public const string PulseCheck = "[Nabiz] >= 0 AND [Nabiz] <= 250";
    public const string DurationCheck = "[SureDakika] >= 0 AND [SureDakika] <= 1440";
    public const string RepetitionsCheck = "[TekrarSayisi] >= 0 AND [TekrarSayisi] <= 1000";

    // Goals (Hedefler): every value is optional; present values follow the measurement limits.
    public const string GoalWeightCheck =
        "([HedefKilo] IS NULL OR ([HedefKilo] >= 1 AND [HedefKilo] <= 200))"
        + " AND ([BaslangicKilo] IS NULL OR ([BaslangicKilo] >= 1 AND [BaslangicKilo] <= 200))"
        + " AND ([MinKilo] IS NULL OR ([MinKilo] >= 1 AND [MinKilo] <= 200))"
        + " AND ([MaxKilo] IS NULL OR ([MaxKilo] >= 1 AND [MaxKilo] <= 200))";
    public const string GoalWeightRangeCheck = "[MinKilo] IS NULL OR [MaxKilo] IS NULL OR [MinKilo] <= [MaxKilo]";
    public const string GoalPulseCheck =
        "([MinNabiz] IS NULL OR ([MinNabiz] >= 1 AND [MinNabiz] <= 250))"
        + " AND ([MaxNabiz] IS NULL OR ([MaxNabiz] >= 1 AND [MaxNabiz] <= 250))";
    public const string GoalPulseRangeCheck = "[MinNabiz] IS NULL OR [MaxNabiz] IS NULL OR [MinNabiz] <= [MaxNabiz]";

    // Reminders (Hatirlaticilar).
    public const int ReminderTitleLength = 100;
    public const string ReminderKindCheck = "[Tur] IN (0, 1)";
    public const string ReminderIntervalCheck = "[AralikSaat] >= 1 AND [AralikSaat] <= 8760";
}
