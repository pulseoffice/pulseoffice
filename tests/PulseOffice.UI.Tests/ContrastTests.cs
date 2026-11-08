using Avalonia.Media;
using PulseOffice.Foundation.Modules;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Tests;

public sealed class ContrastTests
{
    private const double NormalText = 4.5;
    private const double Component = 3.0;

    private readonly WcagColorContrast _contrast = new();

    public static TheoryData<bool> Themes => new() { false, true };

    [Theory]
    [MemberData(nameof(Themes))]
    public void Testo_principale_e_secondario_si_leggono_su_ogni_fondo(bool dark)
    {
        foreach (var key in new[] { Palette.Keys.TextPrimary, Palette.Keys.TextSecondary })
            AssertOnEveryBackground(dark, Palette.Colors(dark)[key], NormalText, key);
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void Il_bordo_dei_campi_si_distingue_su_ogni_fondo(bool dark) =>
        AssertOnEveryBackground(dark, Palette.Colors(dark)[Palette.Keys.FieldBorder], Component, Palette.Keys.FieldBorder);

    [Theory]
    [MemberData(nameof(Themes))]
    public void Il_testo_spento_resta_piu_tenue_del_secondario(bool dark)
    {
        var colors = Palette.Colors(dark);
        var surface = colors[Palette.Keys.Surface];
        Assert.True(_contrast.Ratio(colors[Palette.Keys.TextDisabled], surface) < _contrast.Ratio(colors[Palette.Keys.TextSecondary], surface));
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void L_accento_di_ogni_modulo_si_legge_come_testo_e_regge_il_bianco_come_fondo(bool dark)
    {
        var shades = new ReadableAccentPalette(_contrast);
        foreach (var hex in Enum.GetValues<ModuleKind>().Select(kind => kind == ModuleKind.Shell ? DesignTokens.ShellAccent : ModuleIdentity.For(kind).AccentHex))
        {
            var (fill, text) = shades.For(Color.Parse(hex), dark);
            AssertOnEveryBackground(dark, text, NormalText, $"testo d'accento {hex}");
            AssertOnEveryBackground(dark, fill, Component, $"fondo d'accento {hex}");
            Assert.True(_contrast.Ratio(Colors.White, fill) >= NormalText, $"bianco su {hex}: {_contrast.Ratio(Colors.White, fill):0.00}");
        }
    }

    [Fact]
    public void Il_rapporto_segue_la_formula_WCAG() =>
        Assert.Equal(21.0, _contrast.Ratio(Colors.Black, Colors.White), 3);

    private void AssertOnEveryBackground(bool dark, Color foreground, double minimum, string what)
    {
        var colors = Palette.Colors(dark);
        foreach (var background in Palette.Backgrounds)
        {
            var ratio = _contrast.Ratio(foreground, colors[background]);
            Assert.True(ratio >= minimum, $"{what} su {background} ({(dark ? "scuro" : "chiaro")}): {ratio:0.00} < {minimum}");
        }
    }

    [Fact]
    public void LaMescolaArrivaFinoAlNeroPieno()
    {
        var contrast = new WcagColorContrast();
        var gray = Avalonia.Media.Color.Parse("#808080");

        var readable = contrast.Readable(gray, [gray], 5.3);

        Assert.True(contrast.Ratio(readable, gray) >= 5.3, readable.ToString());
    }
}
