using System.Reflection;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Tests;

public class DesignSystemTests
{
    private static IReadOnlyList<string> DeclaredKeys =>
    [.. typeof(Palette.Keys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)];

    [Fact]
    public void ChiaroEScuroDefinisconoGliStessiColori()
    {
        var light = ColorsOf("Light");
        var dark = ColorsOf("Dark");

        Assert.Equal(light.Keys.Order(), dark.Keys.Order());
    }

    [Fact]
    public void OgniColoreDichiaratoEsisteInEntrambiITemi()
    {
        var light = ColorsOf("Light");
        var dark = ColorsOf("Dark");

        foreach (var key in DeclaredKeys)
        {
            Assert.True(light.ContainsKey(key), $"Manca nel tema chiaro: {key}");
            Assert.True(dark.ContainsKey(key), $"Manca nel tema scuro: {key}");
        }
    }

    [Theory]
    [InlineData(InterfaceDensity.Compact)]
    [InlineData(InterfaceDensity.Comfortable)]
    [InlineData(InterfaceDensity.Touch)]
    public void LaScalaTipograficaCresceSempre(InterfaceDensity density)
    {
        var metrics = DesignTokens.For(density);

        Assert.True(metrics.FontSizeSmall < metrics.FontSize);
        Assert.True(metrics.FontSize < metrics.FontSizeHeading);
        Assert.True(metrics.FontSizeHeading < metrics.FontSizeDisplay);
        Assert.True(metrics.LineHeight > metrics.FontSize);
    }

    [Fact]
    public void LaDensitaCompattaEDavveroLaPiuStretta()
    {
        var compact = DesignTokens.For(InterfaceDensity.Compact);
        var comfortable = DesignTokens.For(InterfaceDensity.Comfortable);
        var touch = DesignTokens.For(InterfaceDensity.Touch);

        Assert.True(compact.RowHeight < comfortable.RowHeight);
        Assert.True(comfortable.RowHeight < touch.RowHeight);
        Assert.True(compact.SpacingMedium < touch.SpacingMedium);
    }

    [Fact]
    public void LeIconeHannoTracciatiValidi()
    {
        var catalog = new IconCatalog();

        foreach (var key in catalog.Keys)
        {
            var icon = catalog.Find(key);
            Assert.NotNull(icon);

            Assert.StartsWith("M", icon!.Path, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void IlTestoHaContrastoSufficienteSulleSueSuperfici(string theme)
    {
        var colors = ColorsOf(theme);

        foreach (var surface in new[] { "PulseSurface", "PulseSurfaceRaised", "PulseSurfaceOverlay" })
        {
            Assert.True(Contrast(colors["PulseTextPrimary"], colors[surface]) >= 4.5,
                $"{theme}: testo primario su {surface} sotto 4,5:1");

            Assert.True(Contrast(colors["PulseTextSecondary"], colors[surface]) >= 3.0,
                $"{theme}: testo secondario su {surface} sotto 3:1");
        }
    }

    private static double Contrast(string first, string second)
    {
        var a = Luminance(first);
        var b = Luminance(second);

        var (light, dark) = a > b ? (a, b) : (b, a);
        return (light + 0.05) / (dark + 0.05);
    }

    private static double Luminance(string hex)
    {
        var value = hex.TrimStart('#');

        double Channel(int offset)
        {
            var raw = Convert.ToInt32(value.Substring(offset, 2), 16) / 255.0;
            return raw <= 0.03928 ? raw / 12.92 : Math.Pow((raw + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(0) + 0.7152 * Channel(2) + 0.0722 * Channel(4);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void IlVeloDelleModaliOscuraDavvero(string theme)
    {
        var scrim = Avalonia.Media.Color.Parse(ColorsOf(theme)[Palette.Keys.Scrim]);

        Assert.InRange(scrim.A, 0x80, 0xE0);
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void PassaggioEPressioneSiVedonoSenzaCoprire(string theme)
    {
        var colors = ColorsOf(theme);

        var hover = Avalonia.Media.Color.Parse(colors[Palette.Keys.Hover]);
        var pressed = Avalonia.Media.Color.Parse(colors[Palette.Keys.Pressed]);

        Assert.InRange(hover.A, 0x08, 0x40);
        Assert.InRange(pressed.A, 0x10, 0x50);
        Assert.True(pressed.A > hover.A, $"{theme}: la pressione non è più marcata del passaggio");
    }

    private static Dictionary<string, string> ColorsOf(string field)
    {
        var value = typeof(Palette)
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null);

        return ((IEnumerable<(string Key, string Color)>)value!)
            .ToDictionary(pair => pair.Key, pair => pair.Color, StringComparer.Ordinal);
    }
}
