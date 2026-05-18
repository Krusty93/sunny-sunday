using Terminal.Gui.Drawing;
using Relego.Core.Branding;

namespace Relego.Cli.Tui;

public enum TuiThemeMode
{
    Dark,
    Light
}

public sealed record TuiPalette(
    TuiThemeMode Mode,
    Color Background,
    Color Text,
    Color TextMuted,
    Color Accent,
    Color AccentMuted,
    Color Border,
    Color BorderFocus,
    Color Success,
    Color Error,
    Color Warning,
    IReadOnlyList<Color> SplashLineColors)
{
    public Color AccentText => Mode == TuiThemeMode.Light ? AccentMuted : Accent;
}

public static class TuiTheme
{
    private const string ThemeEnvironmentVariable = "RELEGO_THEME";

    private static readonly TuiPalette LightPalette = FromBrandPalette(BrandColors.Light, TuiThemeMode.Light);

    private static readonly TuiPalette DarkPalette = FromBrandPalette(BrandColors.Dark, TuiThemeMode.Dark);

    public static TuiPalette Palette => ResolveFromEnvironment(Environment.GetEnvironmentVariable(ThemeEnvironmentVariable));

    public static TuiPalette ResolveFromEnvironment(string? theme)
    {
        return BrandColors.Resolve(theme).Mode == BrandThemeMode.Light
            ? LightPalette
            : DarkPalette;
    }

    private static TuiPalette FromBrandPalette(BrandPalette palette, TuiThemeMode mode)
    {
        static Color ToColor(BrandRgb rgb) => new(rgb.R, rgb.G, rgb.B);

        return new TuiPalette(
            Mode: mode,
            Background: ToColor(palette.Background),
            Text: ToColor(palette.Text),
            TextMuted: ToColor(palette.TextMuted),
            Accent: ToColor(palette.Accent),
            AccentMuted: ToColor(palette.AccentMuted),
            Border: ToColor(palette.Border),
            BorderFocus: ToColor(palette.BorderFocus),
            Success: ToColor(palette.Success),
            Error: ToColor(palette.Error),
            Warning: ToColor(palette.Warning),
            SplashLineColors: [.. palette.SplashLineColors.Select(ToColor)]);
    }

    public static double ContrastRatio(Color foreground, Color background)
        => BrandColors.ContrastRatio(
            new BrandRgb(foreground.R, foreground.G, foreground.B),
            new BrandRgb(background.R, background.G, background.B));
}
