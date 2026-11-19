namespace SaglikTakip.UI.Theming;

/// <summary>
/// Named colors of one theme. Controls never use literal colors; they read a role from the current
/// palette, so the light, dark and high contrast themes stay consistent.
/// </summary>
internal sealed record ThemePalette
{
    public required string Name { get; init; }

    public bool IsDark { get; init; }

    /// <summary>Built from the Windows high contrast colors; custom painting steps back for native drawing.</summary>
    public bool IsHighContrast { get; init; }

    /// <summary>Window background behind cards.</summary>
    public required Color Page { get; init; }

    /// <summary>Cards, inputs, header and footer.</summary>
    public required Color Surface { get; init; }

    /// <summary>Grid headers, alternating rows and quiet areas.</summary>
    public required Color SurfaceAlt { get; init; }

    public required Color SurfaceHover { get; init; }

    /// <summary>Decorative hairlines between areas.</summary>
    public required Color Border { get; init; }

    /// <summary>Borders of inputs; at least 3:1 against the surface.</summary>
    public required Color BorderStrong { get; init; }

    public required Color Text { get; init; }

    public required Color TextSecondary { get; init; }

    public required Color TextMuted { get; init; }

    public required Color TextDisabled { get; init; }

    public required Color Accent { get; init; }

    public required Color AccentHover { get; init; }

    public required Color AccentPressed { get; init; }

    /// <summary>Text and glyphs on <see cref="Accent"/>.</summary>
    public required Color OnAccent { get; init; }

    /// <summary>Tinted background for selected rows and glyph badges.</summary>
    public required Color AccentSubtle { get; init; }

    /// <summary>Accent used as text or a glyph on <see cref="Surface"/>.</summary>
    public required Color AccentText { get; init; }

    public required Color Success { get; init; }

    public required Color SuccessSubtle { get; init; }

    public required Color Warning { get; init; }

    public required Color WarningSubtle { get; init; }

    public required Color Danger { get; init; }

    public required Color DangerSubtle { get; init; }

    public required Color Info { get; init; }

    public required Color InfoSubtle { get; init; }

    /// <summary>Measured values in the trend chart.</summary>
    public required Color ChartValue { get; init; }

    /// <summary>Moving average in the trend chart.</summary>
    public required Color ChartAverage { get; init; }

    /// <summary>Edges of the goal range.</summary>
    public required Color ChartGoal { get; init; }

    /// <summary>Fill between the goal limits; translucent.</summary>
    public required Color ChartGoalBand { get; init; }

    /// <summary>Markers of measurements outside the goal limits.</summary>
    public required Color ChartAlert { get; init; }

    public required Color ChartGrid { get; init; }

    public required Color ChartAxis { get; init; }

    /// <summary>
    /// Light theme. The accent is a deep pine green chosen for this application; the chart series use a
    /// blue/orange pair that stays distinct for color vision deficiencies.
    /// </summary>
    public static ThemePalette Light { get; } = new()
    {
        Name = "Light",
        Page = Hex(0xF3F5F7),
        Surface = Hex(0xFFFFFF),
        SurfaceAlt = Hex(0xF6F8FA),
        SurfaceHover = Hex(0xEDF1F4),
        Border = Hex(0xDDE2E7),
        BorderStrong = Hex(0x8A949E),
        Text = Hex(0x15191E),
        TextSecondary = Hex(0x47515B),
        TextMuted = Hex(0x5F6973),
        TextDisabled = Hex(0x9AA3AC),
        Accent = Hex(0x0B7A5A),
        AccentHover = Hex(0x096B4F),
        AccentPressed = Hex(0x075C44),
        OnAccent = Hex(0xFFFFFF),
        AccentSubtle = Hex(0xE2F2EB),
        AccentText = Hex(0x09684D),
        Success = Hex(0x146B3A),
        SuccessSubtle = Hex(0xE6F4EA),
        Warning = Hex(0x7A4C00),
        WarningSubtle = Hex(0xFFF3D6),
        Danger = Hex(0xB3261E),
        DangerSubtle = Hex(0xFCEBEA),
        Info = Hex(0x1D5FA8),
        InfoSubtle = Hex(0xE8F1FB),
        ChartValue = Hex(0x2A78D6),
        ChartAverage = Hex(0xEB6834),
        ChartGoal = Hex(0x0C8A0C),
        ChartGoalBand = Color.FromArgb(22, 0x0C, 0xA3, 0x0C),
        ChartAlert = Hex(0xD03B3B),
        ChartGrid = Hex(0xE6E9ED),
        ChartAxis = Hex(0xBCC3CA),
    };

    /// <summary>Dark theme: the same roles stepped for a dark surface, not an inverted light theme.</summary>
    public static ThemePalette Dark { get; } = new()
    {
        Name = "Dark",
        IsDark = true,
        Page = Hex(0x111315),
        Surface = Hex(0x1B1E21),
        SurfaceAlt = Hex(0x212529),
        SurfaceHover = Hex(0x2A2F34),
        Border = Hex(0x30353B),
        BorderStrong = Hex(0x737C86),
        Text = Hex(0xECEFF2),
        TextSecondary = Hex(0xB9C1C9),
        TextMuted = Hex(0x99A2AC),
        TextDisabled = Hex(0x5E666E),
        Accent = Hex(0x3DBE8E),
        AccentHover = Hex(0x55CA9E),
        AccentPressed = Hex(0x2FA97C),
        OnAccent = Hex(0x06140F),
        AccentSubtle = Hex(0x17352A),
        AccentText = Hex(0x5BD1A3),
        Success = Hex(0x6FD08C),
        SuccessSubtle = Hex(0x16301F),
        Warning = Hex(0xF2BC55),
        WarningSubtle = Hex(0x33280F),
        Danger = Hex(0xFF9A90),
        DangerSubtle = Hex(0x3A1A18),
        Info = Hex(0x8BBCF7),
        InfoSubtle = Hex(0x172A40),
        ChartValue = Hex(0x3987E5),
        ChartAverage = Hex(0xD95926),
        ChartGoal = Hex(0x0CA30C),
        ChartGoalBand = Color.FromArgb(30, 0x0C, 0xA3, 0x0C),
        ChartAlert = Hex(0xE66767),
        ChartGrid = Hex(0x2C3035),
        ChartAxis = Hex(0x4A5158),
    };

    /// <summary>
    /// High contrast theme built from the active Windows system colors, so the user's contrast theme is
    /// respected instead of being replaced by the application's colors.
    /// </summary>
    public static ThemePalette HighContrast() => new()
    {
        Name = "HighContrast",
        IsHighContrast = true,
        IsDark = SystemColors.Window.GetBrightness() < 0.5f,
        Page = SystemColors.Window,
        Surface = SystemColors.Window,
        SurfaceAlt = SystemColors.Window,
        SurfaceHover = SystemColors.Highlight,
        Border = SystemColors.WindowText,
        BorderStrong = SystemColors.WindowText,
        Text = SystemColors.WindowText,
        TextSecondary = SystemColors.WindowText,
        TextMuted = SystemColors.WindowText,
        TextDisabled = SystemColors.GrayText,
        Accent = SystemColors.Highlight,
        AccentHover = SystemColors.Highlight,
        AccentPressed = SystemColors.Highlight,
        OnAccent = SystemColors.HighlightText,
        AccentSubtle = SystemColors.Window,
        AccentText = SystemColors.HotTrack,
        Success = SystemColors.WindowText,
        SuccessSubtle = SystemColors.Window,
        Warning = SystemColors.WindowText,
        WarningSubtle = SystemColors.Window,
        Danger = SystemColors.WindowText,
        DangerSubtle = SystemColors.Window,
        Info = SystemColors.WindowText,
        InfoSubtle = SystemColors.Window,
        ChartValue = SystemColors.WindowText,
        ChartAverage = SystemColors.HotTrack,
        ChartGoal = SystemColors.GrayText,
        ChartGoalBand = Color.Transparent,
        ChartAlert = SystemColors.Highlight,
        ChartGrid = SystemColors.GrayText,
        ChartAxis = SystemColors.WindowText,
    };

    private static Color Hex(int rgb) => Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
}
