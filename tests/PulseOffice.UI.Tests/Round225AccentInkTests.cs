using Avalonia.Media;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Tests;

public sealed class Round225AccentInkTests
{
    private readonly WcagColorContrast _contrast = new();

    [Theory]
    [InlineData(PaletteVariant.Light)]
    [InlineData(PaletteVariant.Dark)]
    [InlineData(PaletteVariant.HighContrast)]
    public void Text_on_the_danger_badge_is_legible(PaletteVariant variant)
    {
        var colors = Palette.Colors(variant);
        Assert.True(_contrast.Ratio(colors[Palette.Keys.OnDanger], colors[Palette.Keys.Danger]) >= ReadableAccentPalette.TextContrast);
    }

    [Theory]
    [InlineData(PaletteVariant.Light)]
    [InlineData(PaletteVariant.Dark)]
    [InlineData(PaletteVariant.HighContrast)]
    public void Accent_fills_carry_legible_on_accent_text(PaletteVariant variant)
    {
        var palette = new ReadableAccentPalette(_contrast);
        var ink = Color.Parse(Palette.OnAccent);
        foreach (var module in new[] { ModuleKind.Documents, ModuleKind.Sheets, ModuleKind.Presentations })
        {
            var fill = palette.For(Color.Parse(ModuleIdentity.For(module).AccentHex), variant).Fill;
            Assert.True(_contrast.Ratio(ink, fill) >= ReadableAccentPalette.TextContrast);
        }
    }
}
