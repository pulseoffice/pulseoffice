using Avalonia.Media;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Tests;

public sealed class Round220HighContrastTests
{
    private const double Enhanced = 7.0;
    private const double NonText = 3.0;

    private readonly WcagColorContrast _contrast = new();
    private readonly IReadOnlyDictionary<string, Color> _colors = Palette.Colors(PaletteVariant.HighContrast);

    [Fact]
    public void The_high_contrast_palette_defines_every_token_of_the_other_palettes()
    {
        Assert.Equal(Palette.Colors(PaletteVariant.Light).Keys.Order(), _colors.Keys.Order());
    }

    [Theory]
    [InlineData(Palette.Keys.TextPrimary)]
    [InlineData(Palette.Keys.TextSecondary)]
    [InlineData(Palette.Keys.Info)]
    [InlineData(Palette.Keys.Danger)]
    [InlineData(Palette.Keys.Warning)]
    [InlineData(Palette.Keys.Success)]
    public void Text_tokens_reach_the_enhanced_level_on_every_background(string key)
    {
        foreach (var background in Palette.Backgrounds)
            Assert.True(_contrast.Ratio(_colors[key], _colors[background]) >= Enhanced, $"{key} on {background}");
    }

    [Fact]
    public void Chart_ink_and_guides_read_on_the_page()
    {
        Assert.True(_contrast.Ratio(_colors[Palette.Keys.PageInk], _colors[Palette.Keys.Page]) >= Enhanced);
        Assert.True(_contrast.Ratio(_colors[Palette.Keys.PageGuide], _colors[Palette.Keys.Page]) >= NonText);
    }

    [Fact]
    public void The_focus_ring_and_field_borders_stand_out_on_every_background()
    {
        var focus = Color.Parse(Palette.HighContrastFocus);
        foreach (var background in Palette.Backgrounds)
        {
            Assert.True(_contrast.Ratio(focus, _colors[background]) >= NonText);
            Assert.True(_contrast.Ratio(_colors[Palette.Keys.FieldBorder], _colors[background]) >= NonText);
        }
    }

    [Fact]
    public void Module_accents_become_enhanced_text_in_high_contrast()
    {
        var shades = new ReadableAccentPalette(_contrast);
        foreach (var hex in Enum.GetValues<ModuleKind>().Select(kind => kind == ModuleKind.Shell ? DesignTokens.ShellAccent : ModuleIdentity.For(kind).AccentHex))
        {
            var text = shades.For(Color.Parse(hex), PaletteVariant.HighContrast).Text;
            foreach (var background in Palette.Backgrounds)
                Assert.True(_contrast.Ratio(text, _colors[background]) >= Enhanced, $"{hex} on {background}");
        }
    }
}
