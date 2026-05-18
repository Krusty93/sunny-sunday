using Relego.Core.Branding;

namespace Relego.Tests.Tui;

public sealed class BrandColorsTests
{
    [Theory]
    [InlineData("light", BrandThemeMode.Light)]
    [InlineData("LIGHT", BrandThemeMode.Light)]
    [InlineData("dark", BrandThemeMode.Dark)]
    [InlineData("DARK", BrandThemeMode.Dark)]
    [InlineData(null, BrandThemeMode.Dark)]
    [InlineData("unknown", BrandThemeMode.Dark)]
    public void Resolve_ReturnsExpectedThemeMode(string? theme, BrandThemeMode expectedMode)
    {
        var palette = BrandColors.Resolve(theme);

        Assert.Equal(expectedMode, palette.Mode);
    }

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public void Palette_TextAndStatusColors_MeetWcagAaContrast(string theme)
    {
        var palette = BrandColors.Resolve(theme);

        Assert.True(BrandColors.ContrastRatio(palette.Text, palette.Background) >= 4.5d);
        Assert.True(BrandColors.ContrastRatio(palette.TextMuted, palette.Background) >= 4.5d);
        Assert.True(BrandColors.ContrastRatio(palette.AccentText, palette.Background) >= 4.5d);
        Assert.True(BrandColors.ContrastRatio(palette.Success, palette.Background) >= 4.5d);
        Assert.True(BrandColors.ContrastRatio(palette.Error, palette.Background) >= 4.5d);
        Assert.True(BrandColors.ContrastRatio(palette.Warning, palette.Background) >= 4.5d);
    }
}
