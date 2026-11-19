using KutuphaneYonetim.UI.Theming;
using Microsoft.Extensions.Configuration;

namespace KutuphaneYonetim.UI.Tests;

/// <summary>The palettes and tokens stay readable and consistent; the theme setting falls back safely.</summary>
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
        AssertContrast(p.Text, p.SurfaceAlt, 7);
        AssertContrast(p.TextSecondary, p.Surface, 4.5);
        AssertContrast(p.TextSecondary, p.SurfaceAlt, 4.5);
        AssertContrast(p.TextMuted, p.Surface, 4.5);
        AssertContrast(p.TextMuted, p.Page, 4.5);
        AssertContrast(p.OnAccent, p.Accent, 4.5);
        AssertContrast(p.OnAccent, p.AccentHover, 4.5);
        AssertContrast(p.OnAccent, p.AccentPressed, 4.5);
        AssertContrast(p.AccentText, p.Surface, 4.5);
        AssertContrast(p.AccentText, p.AccentSubtle, 4.5);
        AssertContrast(p.Text, p.AccentSubtle, 7);
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void StatusColors_AreReadableInMessagesAndGridRows(string name)
    {
        var p = Palette(name);

        foreach (var (accent, fill) in new[] { (p.Success, p.SuccessSubtle), (p.Warning, p.WarningSubtle), (p.Danger, p.DangerSubtle), (p.Info, p.InfoSubtle) })
        {
            AssertContrast(p.Text, fill, 7);
            AssertContrast(accent, fill, 4.5);
        }

        // Fines, overdue loans, held books and availability are written in these colors on grid rows,
        // including alternating and selected rows.
        foreach (var row in new[] { p.Surface, p.SurfaceAlt, p.AccentSubtle })
        {
            AssertContrast(p.Danger, row, 4.5);
            AssertContrast(p.Success, row, 4.5);
            AssertContrast(p.Warning, row, 4.5);
        }
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void BordersAndAccent_AreDistinguishable(string name)
    {
        var p = Palette(name);

        AssertContrast(p.BorderStrong, p.Surface, 3);
        AssertContrast(p.Accent, p.Surface, 3);
        AssertContrast(p.Accent, p.Page, 3);

        // The primary (accent) and destructive (danger) actions must not look alike.
        Assert.True(HueDistance(p.Accent, p.Danger) >= 15, $"Accent and danger hues are only {HueDistance(p.Accent, p.Danger):0} degrees apart.");
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
    public void Light_AndDark_UseTheSameAccentHue()
    {
        // The dark accent is a lighter step of the same wine, not a different color.
        Assert.InRange(HueDistance(ThemePalette.Light.Accent, ThemePalette.Dark.Accent), 0, 12);
        Assert.True(ThemePalette.Dark.IsDark);
        Assert.False(ThemePalette.Light.IsDark);
    }

    [Fact]
    public void HighContrast_UsesTheSystemColors()
    {
        var p = ThemePalette.HighContrast();

        Assert.True(p.IsHighContrast);
        Assert.Equal(SystemColors.WindowText, p.Text);
        Assert.Equal(SystemColors.Window, p.Surface);
        Assert.Equal(SystemColors.Highlight, p.Accent);
        Assert.Equal(SystemColors.HighlightText, p.OnAccent);
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
        Assert.True(Typography.Caption.Size >= 9F, "Captions must not be smaller than 9 pt.");
        Assert.True(Radius.Control <= Radius.Card);
        Assert.True(Sizes.ControlHeight >= 24, "Click targets need at least 24 logical pixels.");
        Assert.True(Sizes.ButtonMinWidth >= Sizes.ControlHeight);
    }

    [Fact]
    public void Glyphs_ArePrivateUseCharacters()
    {
        var glyphs = typeof(Glyphs).GetFields()
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (field.Name, Value: (string)field.GetRawConstantValue()!))
            .ToList();

        Assert.NotEmpty(glyphs);
        Assert.All(glyphs, glyph => Assert.True(
            glyph.Value.Length == 1 && glyph.Value[0] is >= '' and <= '',
            $"{glyph.Name} is not a single icon font character."));
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
    public void ExampleSettings_UseTheLightTheme()
    {
        var configuration = new ConfigurationBuilder()
            .AddSettingsFile(AppContext.BaseDirectory, "appsettings.example.json")
            .Build();

        Assert.Equal("Light", configuration[AppConfiguration.ThemeKey]);
    }

    [Fact]
    public void ContrastRatio_MatchesWcagReferenceValues()
    {
        Assert.Equal(21, Theme.ContrastRatio(Color.Black, Color.White), 3);
        Assert.Equal(1, Theme.ContrastRatio(Color.White, Color.White), 3);
        Assert.Equal(Theme.ContrastRatio(Color.Navy, Color.White), Theme.ContrastRatio(Color.White, Color.Navy), 9);
    }

    [Fact]
    public void AppIcon_IsTheEmbeddedLogo()
    {
        Assert.NotSame(SystemIcons.Application, Theme.AppIcon);
        Assert.Equal(256, Theme.Logo.Width);
    }

    private static ThemePalette Palette(string name) => name == "Dark" ? ThemePalette.Dark : ThemePalette.Light;

    private static double HueDistance(Color first, Color second)
    {
        var distance = Math.Abs(first.GetHue() - second.GetHue());
        return Math.Min(distance, 360 - distance);
    }

    private static void AssertContrast(Color foreground, Color background, double minimum)
    {
        var ratio = Theme.ContrastRatio(foreground, background);
        Assert.True(ratio >= minimum, $"Contrast of {Hex(foreground)} on {Hex(background)} is {ratio:0.00}, expected at least {minimum}.");
    }

    private static string Hex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
}
