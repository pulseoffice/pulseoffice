using PulseOffice.Charts;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DeckChartReferenceSample
{
    public const string FolderVariable = "PULSEOFFICE_CHART_SAMPLES";

    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    [Fact]
    public void ScriveIGraficiPerLaVerificaEsterna()
    {
        if (Environment.GetEnvironmentVariable(FolderVariable) is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        using (var odp = File.Create(Path.Combine(folder, "charts.odp"))) Assert.True(new OdpWriter().Write(Deck(), odp).Succeeded);
        using (var pptx = File.Create(Path.Combine(folder, "charts.pptx"))) Assert.True(new PptxWriter().Write(Deck(), pptx).Succeeded);
    }

    [Theory]
    [InlineData("from-odp.pptx")]
    [InlineData("from-pptx.odp")]
    [InlineData("charts.odp")]
    [InlineData("charts.pptx")]
    public void IGraficiConvertitiDaUnAltraSuiteSiRileggono(string name)
    {
        if (Environment.GetEnvironmentVariable(FolderVariable) is not { Length: > 0 } folder) return;
        var path = Path.Combine(folder, name);
        if (!File.Exists(path)) return;
        using var stream = File.OpenRead(path);
        var result = name.EndsWith(".odp", StringComparison.Ordinal)
            ? new OdpReader(_themes, _masters).Read(stream)
            : new PptxReader(_themes, _masters).Read(stream);
        Assert.True(result.Succeeded);
        var charts = result.Value!.Slides.SelectMany(slide => slide.Elements).OfType<ObjectElement>()
            .Select(element => element.Content).OfType<ChartDefinition>().ToList();
        File.WriteAllLines(path + ".report.txt", charts.Select(chart =>
            $"{chart.Type} | {chart.ChartTitle} | {string.Join(",", chart.InlineData?.Categories ?? [])} | "
            + string.Join(" ; ", (chart.InlineData?.Series ?? []).Select(series => series.Name + "=" + string.Join(",", series.Values)))));
        Assert.Equal(3, charts.Count);
        Assert.All(charts, chart => Assert.Equal(3, chart.InlineData!.Categories.Count));
    }

    private Presentation Deck()
    {
        var theme = _themes.Find("Light")!;
        var master = _masters.CreateStandard(theme);
        var deck = new Presentation { Theme = theme, Master = master, Size = SlideSize.Widescreen };
        Add(deck, master, ChartType.Column, "Istogramma", [new ChartSeries("Nord", [10, 20, 15]), new ChartSeries("Sud", [5, double.NaN, 12])]);
        Add(deck, master, ChartType.Pie, "Torta", [new ChartSeries("Quote", [40, 35, 25])]);
        Add(deck, master, ChartType.Line, "Linee", [new ChartSeries("Trend", [3, double.NaN, 9])]);
        return deck;
    }

    private void Add(Presentation deck, SlideMaster master, ChartType type, string title, IReadOnlyList<ChartSeries> series)
    {
        var slide = _slides.Create(deck.NextId(), master, SlideLayout.TitleOnly);
        var id = "chart" + deck.Slides.Count;
        slide.Add(new ObjectElement
        {
            Id = id,
            ZOrder = 5,
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(4), Twip.FromCentimeters(20), Twip.FromCentimeters(12)),
            Content = new ChartDefinition
            {
                Id = id, Type = type, SourceSheet = "", SourceRange = "", ChartTitle = title,
                InlineData = new ChartData(["Gen", "Feb", "Mar"], series),
            },
        });
        deck.AddExisting(slide);
    }
}
