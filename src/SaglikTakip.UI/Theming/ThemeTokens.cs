namespace SaglikTakip.UI.Theming;

/// <summary>Spacing scale in logical pixels (96 DPI); layouts are scaled by Windows Forms for the monitor.</summary>
internal static class Spacing
{
    public const int XS = 4;
    public const int S = 8;
    public const int M = 12;
    public const int L = 16;
    public const int XL = 24;
    public const int XXL = 32;

    /// <summary>All steps in ascending order.</summary>
    public static IReadOnlyList<int> Scale { get; } = [XS, S, M, L, XL, XXL];
}

/// <summary>Corner radii in logical pixels.</summary>
internal static class Radius
{
    /// <summary>Buttons and inputs.</summary>
    public const int Control = 4;

    /// <summary>Cards, banners and tiles.</summary>
    public const int Card = 8;
}

/// <summary>Fixed control sizes in logical pixels.</summary>
internal static class Sizes
{
    /// <summary>Height of buttons; inputs follow their font.</summary>
    public const int ControlHeight = 32;

    public const int ButtonMinWidth = 96;

    /// <summary>Glyph badge in dialog headers.</summary>
    public const int HeaderBadge = 40;

    /// <summary>Glyph badge on the main menu tiles.</summary>
    public const int TileBadge = 40;

    public const int TileHeight = 96;

    public const int FocusRing = 2;
}

/// <summary>One step of the type ramp; sizes are in points.</summary>
internal readonly record struct FontToken(string Family, float Size, FontStyle Style = FontStyle.Regular);

/// <summary>
/// Type ramp: Segoe UI throughout (the Windows UI face), Semibold for hierarchy instead of bold or color.
/// Display &gt; Title &gt; Subtitle &gt; Body &gt; Caption.
/// </summary>
internal static class Typography
{
    public const string Family = "Segoe UI";
    public const string SemiboldFamily = "Segoe UI Semibold";

    public static readonly FontToken Display = new(SemiboldFamily, 18F);
    public static readonly FontToken Title = new(SemiboldFamily, 13.5F);
    public static readonly FontToken Subtitle = new(SemiboldFamily, 11F);
    public static readonly FontToken Body = new(Family, 9.75F);
    public static readonly FontToken BodyStrong = new(SemiboldFamily, 9.75F);
    public static readonly FontToken Caption = new(Family, 9F);
    public static readonly FontToken Value = new(SemiboldFamily, 14F);
    public static readonly FontToken Mono = new("Consolas", 10F);

    /// <summary>Text sizes from the smallest to the largest; used to check the ramp is ordered.</summary>
    public static IReadOnlyList<FontToken> Ramp { get; } = [Caption, Body, Subtitle, Title, Display];
}

/// <summary>Code points of Segoe Fluent Icons / Segoe MDL2 Assets used by the application.</summary>
internal static class Glyphs
{
    public const string AddFriend = "";
    public const string Heart = "";
    public const string Run = "";
    public const string Document = "";
    public const string Chart = "";
    public const string Target = "";
    public const string Bell = "";
    public const string Calendar = "";
    public const string Info = "";
    public const string Success = "";
    public const string Warning = "";
    public const string Error = "";
    public const string ChevronDown = "";
    public const string Sync = "";
    public const string Search = "";
}
