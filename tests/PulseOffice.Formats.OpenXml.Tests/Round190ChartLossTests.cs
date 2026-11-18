using PulseOffice.Charts;
using PulseOffice.Documents;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round190ChartLossTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly IThemeCatalog _themes = new ThemeCatalog();
    private readonly IMasterFactory _masters = new MasterFactory();
    private readonly ISlideFactory _slides = new SlideFactory();

    private static ChartDefinition Chart(ChartType type, ChartSeriesFormat? format = null, double? categoryUnit = null) => new()
    {
        Id = "c1", Type = type, SourceSheet = string.Empty, SourceRange = string.Empty,
        InlineData = new ChartData(["A", "B", "C"], [new ChartSeries("S", [1, 2, 3])]),
        SeriesFormats = format is null ? null : [format],
        CategoryAxis = new ChartAxisScale { MajorUnit = categoryUnit },
    };

    private static readonly ChartSeriesFormat Trend = new() { Trendline = new ChartTrendline(TrendlineKind.Linear) };

    private static readonly ChartSeriesFormat Dashed = new() { Dash = LineDash.Dash };

    private TextDocument Folio(ChartDefinition chart)
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        document.Objects.Add(new AnchoredObject("o1", new PageAnchor(1), new EmbeddedContent(chart),
            new DocumentSize(Twip.FromCentimeters(8), Twip.FromCentimeters(5))));
        return document;
    }

    private Presentation Deck(ChartDefinition chart)
    {
        var master = _masters.CreateStandard(_themes.Default);
        var deck = new Presentation { Theme = _themes.Default, Master = master };
        deck.AddExisting(_slides.Create(deck.NextId(), master, SlideLayout.Blank));
        deck.Slides[0].Add(new ObjectElement
        {
            Id = "g1",
            Bounds = new DocumentRect(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(10), Twip.FromCentimeters(6)),
            Content = chart,
        });
        return deck;
    }

    private IReadOnlyList<string> Docx(ChartDefinition chart) =>
        [.. _composer.Writer(DocxFlavour.Document).CheckFidelity(Folio(chart)).Losses.Select(loss => loss.Code)];

    private IReadOnlyList<string> Pptx(ChartDefinition chart) =>
        [.. new PptxWriter().CheckFidelity(Deck(chart)).Losses.Select(loss => loss.Code)];

    [Fact]
    public void Docx_declares_dropped_series_analysis_and_category_units()
    {
        Assert.Contains("DOCX241", Docx(Chart(ChartType.Pie, Trend)));
        Assert.DoesNotContain("DOCX241", Docx(Chart(ChartType.Column, Trend)));
        Assert.Contains("DOCX242", Docx(Chart(ChartType.Column, categoryUnit: 2)));
        Assert.DoesNotContain("DOCX242", Docx(Chart(ChartType.Column)));
    }

    [Fact]
    public void Pptx_declares_dropped_series_analysis_and_category_units()
    {
        Assert.Contains("PPTX241", Pptx(Chart(ChartType.Radar, Trend)));
        Assert.DoesNotContain("PPTX241", Pptx(Chart(ChartType.Line, Trend)));
        Assert.Contains("PPTX242", Pptx(Chart(ChartType.Line, categoryUnit: 2)));
        Assert.DoesNotContain("PPTX242", Pptx(Chart(ChartType.Line)));
    }

    [Fact]
    public void Odt_and_odp_declare_chart_extensions()
    {
        Assert.Contains(new OdtWriter().CheckFidelity(Folio(Chart(ChartType.Line, Dashed))).Losses, loss => loss.Code == "ODT287");
        Assert.DoesNotContain(new OdtWriter().CheckFidelity(Folio(Chart(ChartType.Line))).Losses, loss => loss.Code == "ODT287");
        Assert.Contains(new OdpWriter().CheckFidelity(Deck(Chart(ChartType.Line, Dashed))).Losses, loss => loss.Code == "ODP287");
        Assert.DoesNotContain(new OdpWriter().CheckFidelity(Deck(Chart(ChartType.Line))).Losses, loss => loss.Code == "ODP287");
    }
}
