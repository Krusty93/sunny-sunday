namespace Relego.Core.Branding;

public enum BrandThemeMode
{
    Dark,
    Light
}

public readonly record struct BrandRgb(byte R, byte G, byte B);

public sealed record BrandPalette(
    BrandThemeMode Mode,
    BrandRgb Background,
    BrandRgb Text,
    BrandRgb TextMuted,
    BrandRgb Accent,
    BrandRgb AccentMuted,
    BrandRgb Border,
    BrandRgb BorderFocus,
    BrandRgb Success,
    BrandRgb Error,
    BrandRgb Warning,
    IReadOnlyList<BrandRgb> SplashLineColors)
{
    public BrandRgb AccentText => Mode == BrandThemeMode.Light ? AccentMuted : Accent;
}

public static class BrandColors
{
    public static readonly BrandPalette Light = new(
        Mode: BrandThemeMode.Light,
        Background: new BrandRgb(247, 241, 232),
        Text: new BrandRgb(23, 19, 17),
        TextMuted: new BrandRgb(91, 79, 71),
        Accent: new BrandRgb(181, 107, 57),
        AccentMuted: new BrandRgb(129, 82, 48),
        Border: new BrandRgb(220, 214, 206),
        BorderFocus: new BrandRgb(181, 107, 57),
        Success: new BrandRgb(47, 111, 68),
        Error: new BrandRgb(154, 47, 42),
        Warning: new BrandRgb(138, 92, 24),
        SplashLineColors:
        [
            new BrandRgb(181, 107, 57),
            new BrandRgb(163, 96, 52),
            new BrandRgb(144, 85, 47),
            new BrandRgb(126, 74, 43),
            new BrandRgb(108, 63, 38),
            new BrandRgb(90, 52, 33)
        ]);

    public static readonly BrandPalette Dark = new(
        Mode: BrandThemeMode.Dark,
        Background: new BrandRgb(18, 14, 12),
        Text: new BrandRgb(245, 238, 227),
        TextMuted: new BrandRgb(178, 167, 151),
        Accent: new BrandRgb(212, 160, 94),
        AccentMuted: new BrandRgb(165, 124, 72),
        Border: new BrandRgb(50, 45, 42),
        BorderFocus: new BrandRgb(212, 160, 94),
        Success: new BrandRgb(120, 201, 143),
        Error: new BrandRgb(255, 154, 143),
        Warning: new BrandRgb(224, 184, 106),
        SplashLineColors:
        [
            new BrandRgb(212, 160, 94),
            new BrandRgb(223, 176, 116),
            new BrandRgb(233, 192, 138),
            new BrandRgb(245, 208, 160),
            new BrandRgb(255, 222, 180),
            new BrandRgb(245, 208, 160)
        ]);

    public static BrandPalette Resolve(string? theme)
    {
        if (string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase))
        {
            return Light;
        }

        if (string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase))
        {
            return Dark;
        }

        return Dark;
    }

    public static double ContrastRatio(BrandRgb foreground, BrandRgb background)
    {
        var fg = RelativeLuminance(foreground);
        var bg = RelativeLuminance(background);

        var light = Math.Max(fg, bg);
        var dark = Math.Min(fg, bg);
        return (light + 0.05d) / (dark + 0.05d);
    }

    private static double RelativeLuminance(BrandRgb color)
    {
        var r = Linearize(color.R / 255d);
        var g = Linearize(color.G / 255d);
        var b = Linearize(color.B / 255d);
        return (0.2126d * r) + (0.7152d * g) + (0.0722d * b);
    }

    private static double Linearize(double channel)
        => channel <= 0.03928d
            ? channel / 12.92d
            : Math.Pow((channel + 0.055d) / 1.055d, 2.4d);
}
