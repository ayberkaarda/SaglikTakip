using System.Drawing.Text;
using Microsoft.Win32;

namespace SaglikTakip.UI.Theming;

/// <summary>
/// The active theme: palette, fonts and the application icon. It is chosen once at startup from the
/// settings, the Windows app mode and the high contrast setting.
/// </summary>
internal static class Theme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string IconResource = "SaglikTakip.UI.app.ico";

    private static ThemeFonts? _fonts;
    private static Icon? _icon;
    private static Bitmap? _logo;
    private static string? _glyphFamily;
    private static bool _glyphFamilyResolved;

    public static ThemePalette Current { get; private set; } = ThemePalette.Light;

    /// <summary>
    /// Extra factor on top of the monitor DPI for fonts and painted metrics. It stays 1 in the application;
    /// the offscreen screenshot tool sets 2 to render a 2x image on a 96 DPI desktop.
    /// </summary>
    public static float RenderScale { get; private set; } = 1F;

    public static ThemeFonts Fonts => _fonts ??= new ThemeFonts(RenderScale);

    /// <summary>Selects the palette; existing windows are not repainted.</summary>
    public static void Initialize(ThemeMode mode, bool highContrast, bool systemPrefersDark) =>
        Use(Resolve(mode, highContrast, systemPrefersDark));

    /// <summary>Activates a palette and render scale; fonts are recreated on next use.</summary>
    public static void Use(ThemePalette palette, float renderScale = 1F)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentOutOfRangeException.ThrowIfLessThan(renderScale, 0.5F);

        Current = palette;
        if (Math.Abs(renderScale - RenderScale) > 0.001F)
        {
            RenderScale = renderScale;
            _fonts = null; // Fonts that are still used by open controls are not disposed.
        }
    }

    /// <summary>High contrast always wins; otherwise the setting, with System following the Windows app mode.</summary>
    public static ThemePalette Resolve(ThemeMode mode, bool highContrast, bool systemPrefersDark)
    {
        if (highContrast)
        {
            return ThemePalette.HighContrast();
        }

        return mode switch
        {
            ThemeMode.Dark => ThemePalette.Dark,
            ThemeMode.System when systemPrefersDark => ThemePalette.Dark,
            _ => ThemePalette.Light,
        };
    }

    /// <summary>Reads the Windows "app mode" (light or dark); light when it cannot be read.</summary>
    public static bool SystemPrefersDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    /// <summary>Logical pixels to device pixels for <paramref name="control"/>, including <see cref="RenderScale"/>.</summary>
    public static int Px(Control control, float logical)
    {
        ArgumentNullException.ThrowIfNull(control);
        return (int)Math.Round(logical * control.DeviceDpi / 96F * RenderScale);
    }

    /// <summary>Same as <see cref="Px(Control, float)"/> as a float, for painting.</summary>
    public static float PxF(Control control, float logical)
    {
        ArgumentNullException.ThrowIfNull(control);
        return logical * control.DeviceDpi / 96F * RenderScale;
    }

    /// <summary>The application icon (generated from assets/logo.svg).</summary>
    public static Icon AppIcon => _icon ??= LoadIcon() ?? SystemIcons.Application;

    /// <summary>The largest frame of the application icon, for painting the logo.</summary>
    public static Bitmap Logo => _logo ??= new Icon(AppIcon, new Size(256, 256)).ToBitmap();

    /// <summary>Segoe Fluent Icons (Windows 11) or Segoe MDL2 Assets (Windows 10); null when neither exists.</summary>
    public static string? GlyphFamily
    {
        get
        {
            if (!_glyphFamilyResolved)
            {
                using var installed = new InstalledFontCollection();
                var names = installed.Families.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                _glyphFamily = names.Contains("Segoe Fluent Icons") ? "Segoe Fluent Icons"
                    : names.Contains("Segoe MDL2 Assets") ? "Segoe MDL2 Assets"
                    : null;
                _glyphFamilyResolved = true;
            }

            return _glyphFamily;
        }
    }

    /// <summary>WCAG contrast ratio of two opaque colors (1 to 21).</summary>
    public static double ContrastRatio(Color first, Color second)
    {
        var a = RelativeLuminance(first);
        var b = RelativeLuminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>Blends <paramref name="color"/> over <paramref name="background"/> by its alpha.</summary>
    public static Color Flatten(Color color, Color background)
    {
        var alpha = color.A / 255.0;
        return Color.FromArgb(
            (int)Math.Round((color.R * alpha) + (background.R * (1 - alpha))),
            (int)Math.Round((color.G * alpha) + (background.G * (1 - alpha))),
            (int)Math.Round((color.B * alpha) + (background.B * (1 - alpha))));
    }

    private static double RelativeLuminance(Color color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
    }

    private static Icon? LoadIcon()
    {
        using var stream = typeof(Theme).Assembly.GetManifestResourceStream(IconResource);
        return stream is null ? null : new Icon(stream);
    }
}

/// <summary>Fonts of the type ramp at one render scale.</summary>
internal sealed class ThemeFonts
{
    public ThemeFonts(float scale)
    {
        Display = Create(Typography.Display, scale);
        Title = Create(Typography.Title, scale);
        Subtitle = Create(Typography.Subtitle, scale);
        Body = Create(Typography.Body, scale);
        BodyStrong = Create(Typography.BodyStrong, scale);
        Caption = Create(Typography.Caption, scale);
        Value = Create(Typography.Value, scale);
        Mono = Create(Typography.Mono, scale);
        Scale = scale;
    }

    public float Scale { get; }

    public Font Display { get; }

    public Font Title { get; }

    public Font Subtitle { get; }

    public Font Body { get; }

    public Font BodyStrong { get; }

    public Font Caption { get; }

    public Font Value { get; }

    public Font Mono { get; }

    /// <summary>An icon font of <paramref name="points"/> size, or null when no icon font is installed.</summary>
    public Font? Glyph(float points)
    {
        if (Theme.GlyphFamily is not { } family)
        {
            return null;
        }

        lock (_glyphs)
        {
            if (!_glyphs.TryGetValue(points, out var font))
            {
                font = new Font(family, points * Scale, FontStyle.Regular, GraphicsUnit.Point);
                _glyphs[points] = font;
            }

            return font;
        }
    }

    private readonly Dictionary<float, Font> _glyphs = [];

    private static Font Create(FontToken token, float scale) =>
        new(token.Family, token.Size * scale, token.Style, GraphicsUnit.Point);
}
