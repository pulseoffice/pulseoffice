using PulseOffice.UI.Icons;
using Xunit;

namespace PulseOffice.UI.Tests;

public class IconCatalogTests
{
    private static IReadOnlyList<IIconCatalog> Catalogs =>
    [
        new IconCatalog(), new CellIcons(), new DataIcons(), new DataDialogIcons(), new FormulaIcons(),
        new DrawingIcons(), new ViewIcons(), new ReviewIcons(), new ToolIcons(), new PageIcons(), new RibbonControlIcons(),
            new ChartIcons(), new PanelIcons(), new DeckIcons(), new FolioIcons(), new SettingsIcons(), new TextFormatIcons(),
    ];

    [Fact]
    public void NessunaChiaveCompareInDueCataloghi()
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var catalog in Catalogs)
            foreach (var key in catalog.Keys)
                seen[key] = seen.GetValueOrDefault(key) + 1;

        Assert.Empty(seen.Where(entry => entry.Value > 1).Select(entry => entry.Key));
    }

    [Fact]
    public void OgniTracciatoComminciaConUnoSpostamento()
    {
        foreach (var catalog in Catalogs)
            foreach (var key in catalog.Keys)
                Assert.StartsWith("M", catalog.Find(key)!.Path, StringComparison.Ordinal);
    }

    [Fact]
    public void IPuntiAssolutiRestanoDentroLaGriglia()
    {
        foreach (var catalog in Catalogs)
            foreach (var key in catalog.Keys)
                foreach (var number in AbsolutePoints(catalog.Find(key)!.Path))
                    Assert.InRange(number, 0, IconGeometry.Grid);
    }

    private static IEnumerable<double> AbsolutePoints(string path) =>
        System.Text.RegularExpressions.Regex
            .Matches(path, @"[ML]\s*(-?\d+(\.\d+)?)\s+(-?\d+(\.\d+)?)")
            .SelectMany(match => new[] { match.Groups[1].Value, match.Groups[3].Value })
            .Select(value => double.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
}
