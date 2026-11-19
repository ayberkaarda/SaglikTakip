using System.Drawing;
using Microsoft.Extensions.Configuration;
using SaglikTakip.UI.Theming;

namespace SaglikTakip.UI.Tests;

public sealed class ThemeTests
{
    public static TheoryData<string> Palettes => ["Light", "Dark"];

    [Theory]
    [MemberData(nameof(Palettes))]
    public void TextRoles_MeetWcagContrast(string name)
    {
        var p = Palette(name);

        AssertContrast(p.Text, p.Surface, 7);
        AssertContrast(p.Text, p.Page, 7);
        AssertContrast(p.TextSecondary, p.Surface, 4.5);
        AssertContrast(p.TextMuted, p.Surface, 4.5);
        AssertContrast(p.TextMuted, p.Page, 4.5);
        AssertContrast(p.OnAccent, p.Accent, 4.5);
        AssertContrast(p.OnAccent, p.AccentHover, 4.5);
        AssertContrast(p.AccentText, p.Surface, 4.5);
        AssertContrast(p.Text, p.AccentSubtle, 4.5);
        AssertContrast(p.Danger, p.Surface, 4.5);
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void StatusMessages_AreReadableOnTheirBackground(string name)
    {
        var p = Palette(name);

        foreach (var (accent, fill) in new[] { (p.Success, p.SuccessSubtle), (p.Warning, p.WarningSubtle), (p.Danger, p.DangerSubtle), (p.Info, p.InfoSubtle) })
        {
            AssertContrast(p.Text, fill, 7);
            AssertContrast(accent, fill, 4.5);
        }

        // Due reminders are marked with these colors in a grid row.
        AssertContrast(p.Warning, p.Surface, 4.5);
        AssertContrast(p.Warning, p.SurfaceAlt, 4.5);
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void BordersAndChartMarks_AreDistinguishable(string name)
    {
        var p = Palette(name);

        AssertContrast(p.BorderStrong, p.Surface, 3);
        AssertContrast(p.ChartValue, p.Surface, 3);
        AssertContrast(p.ChartAverage, p.Surface, 3);
        AssertContrast(p.ChartAlert, p.Surface, 3);
        AssertContrast(p.ChartGoal, p.Surface, 3);

        // Axis labels and the latest value are drawn on top of the goal band.
        var band = Theme.Flatten(p.ChartGoalBand, p.Surface);
        AssertContrast(p.TextMuted, band, 4.5);
        AssertContrast(p.Text, band, 7);
    }

    [Fact]
    public void Palettes_DefineEveryRole()
    {
        foreach (var palette in new[] { ThemePalette.Light, ThemePalette.Dark, ThemePalette.HighContrast() })
        {
            var colors = typeof(ThemePalette).GetProperties()
                .Where(property => property.PropertyType == typeof(Color))
                .Select(property => (property.Name, Value: (Color)property.GetValue(palette)!))
                .ToList();

            Assert.NotEmpty(colors);
            Assert.All(colors, color => Assert.False(color.Value.IsEmpty, $"{palette.Name}.{color.Name} is not set."));
        }
    }

    [Fact]
    public void Light_AndDark_UseTheSameSeriesHues()
    {
        // Light and dark are stepped versions of the same series colors: blue stays blue, orange stays orange.
        Assert.InRange(Math.Abs(ThemePalette.Light.ChartValue.GetHue() - ThemePalette.Dark.ChartValue.GetHue()), 0, 10);
        Assert.InRange(Math.Abs(ThemePalette.Light.ChartAverage.GetHue() - ThemePalette.Dark.ChartAverage.GetHue()), 0, 10);
        Assert.True(ThemePalette.Dark.IsDark);
        Assert.False(ThemePalette.Light.IsDark);
    }

    [Fact]
    public void Tokens_AreOrderedScales()
    {
        Assert.Equal(Spacing.Scale.Order(), Spacing.Scale);
        Assert.Equal(Spacing.Scale.Count, Spacing.Scale.Distinct().Count());
        Assert.All(Spacing.Scale, step => Assert.Equal(0, step % 4));

        var sizes = Typography.Ramp.Select(token => token.Size).ToList();
        Assert.Equal(sizes.Order(), sizes);
        Assert.Equal(sizes.Count, sizes.Distinct().Count());
        Assert.True(Typography.Body.Size >= 9F, "Body text must not be smaller than 9 pt.");
        Assert.True(Radius.Control <= Radius.Card);
        Assert.True(Sizes.ControlHeight >= 24, "Click targets need at least 24 logical pixels.");
    }

    [Theory]
    [InlineData(ThemeMode.Light, false, false, "Light")]
    [InlineData(ThemeMode.Light, false, true, "Light")]
    [InlineData(ThemeMode.Dark, false, false, "Dark")]
    [InlineData(ThemeMode.System, false, true, "Dark")]
    [InlineData(ThemeMode.System, false, false, "Light")]
    [InlineData(ThemeMode.Dark, true, true, "HighContrast")]
    [InlineData(ThemeMode.Light, true, false, "HighContrast")]
    public void Resolve_HighContrastWinsThenTheSetting(ThemeMode mode, bool highContrast, bool systemDark, string expected)
    {
        var palette = Theme.Resolve(mode, highContrast, systemDark);

        Assert.Equal(expected, palette.Name);
        Assert.Equal(highContrast, palette.IsHighContrast);
    }

    [Theory]
    [InlineData("dark", ThemeMode.Dark)]
    [InlineData("System", ThemeMode.System)]
    [InlineData("LIGHT", ThemeMode.Light)]
    [InlineData(null, ThemeMode.Light)]
    [InlineData("", ThemeMode.Light)]
    [InlineData("neon", ThemeMode.Light)]
    [InlineData("7", ThemeMode.Light)]
    public void ThemeSetting_FallsBackToLight(string? value, ThemeMode expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AppConfiguration.ThemeKey] = value })
            .Build();

        Assert.Equal(expected, AppConfiguration.ResolveTheme(configuration));
    }

    [Fact]
    public void ContrastRatio_MatchesWcagReferenceValues()
    {
        Assert.Equal(21, Theme.ContrastRatio(Color.Black, Color.White), 3);
        Assert.Equal(1, Theme.ContrastRatio(Color.White, Color.White), 3);
        Assert.Equal(Theme.ContrastRatio(Color.Navy, Color.White), Theme.ContrastRatio(Color.White, Color.Navy), 9);
    }

    private static ThemePalette Palette(string name) => name == "Dark" ? ThemePalette.Dark : ThemePalette.Light;

    private static void AssertContrast(Color foreground, Color background, double minimum)
    {
        var ratio = Theme.ContrastRatio(foreground, background);
        Assert.True(ratio >= minimum, $"Contrast of {Hex(foreground)} on {Hex(background)} is {ratio:0.00}, expected at least {minimum}.");
    }

    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
