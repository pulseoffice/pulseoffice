using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml.Tests;

public class DeepAuditRound28ChartTests
{
    private ChartDefinition ThroughPart(ChartDefinition chart, Action<C.ChartSpace>? alter = null)
    {
        var parts = new InlineChartParts();
        using var stream = new MemoryStream();
        using var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        var id = parts.Write(main, chart);
        if (alter is not null)
        {
            var part = (ChartPart)main.GetPartById(id);
            alter(part.ChartSpace!);
            part.ChartSpace!.Save();
        }
        return parts.Read(main, id, chart.Id)!;
    }

    [Fact]
    public void INomiEIColoriDiUnaBollaRestanoSulleSerieGiuste()
    {
        var read = ThroughPart(new ChartDefinition
        {
            Id = "b", Type = ChartType.Bubble, SourceSheet = string.Empty, SourceRange = string.Empty,
            SeriesNames = [null!, "Fatturato", null!],
            SeriesColors = [null, "#123456", null],
            InlineData = new ChartData(["a", "b"], [new ChartSeries("X", [1, 2]), new ChartSeries("Y", [5, 7]), new ChartSeries("Size", [3, 9])]),
        });

        Assert.Null(read.ExplicitSeriesName(0));
        Assert.Equal("Fatturato", read.ExplicitSeriesName(1));
        Assert.Null(read.ExplicitSeriesColor(0));
        Assert.Equal("#123456", read.ExplicitSeriesColor(1));
    }

    [Fact]
    public void IlNomeEsplicitoDiUnaSerieInLineaSiScriveInPptxEDocx()
    {
        var read = ThroughPart(new ChartDefinition
        {
            Id = "n", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty,
            SeriesNames = ["Ricavi"],
            InlineData = new ChartData(["a"], [new ChartSeries("Serie 1", [1])]),
        });

        Assert.Equal("Ricavi", read.ExplicitSeriesName(0));
    }

    [Fact]
    public void IlTitoloCollegatoAUnaCellaSiLeggeDallaCache()
    {
        var read = ThroughPart(new ChartDefinition
        {
            Id = "t", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty,
            InlineData = new ChartData(["a"], [new ChartSeries("S", [1])]),
        }, space =>
        {
            var chart = space.GetFirstChild<C.Chart>()!;
            chart.InsertAt(new C.Title(new C.ChartText(new C.StringReference(
                new C.Formula("Foglio1!$A$1"),
                new C.StringCache(new C.PointCount { Val = 1u }, new C.StringPoint(new C.NumericValue("Vendite 2026")) { Index = 0u })))),
                0);
        });

        Assert.Equal("Vendite 2026", read.ChartTitle);
    }

    [Fact]
    public void IValoriSenzaCategorieSopravvivonoAllOdp()
    {
        var mapper = new OdfDeckChartMapper();
        var data = new ChartData([], [new ChartSeries("S", [4, 5, 6])]);
        var definition = new ChartDefinition { Id = "c", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty, InlineData = data };
        var document = mapper.BuildDocument(definition, data, new DocumentSize(Twip.FromCentimeters(10), Twip.FromCentimeters(6)), System.Globalization.CultureInfo.InvariantCulture);
        var frame = mapper.BuildFrame(new ObjectElement { Id = "c", Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(2000), new Twip(2000)), Content = definition }, "./Object 1");

        var read = mapper.Read(frame, "c", _ => XDocument.Parse(document.ToString()));

        var chart = Assert.IsType<ChartDefinition>(read!.Content);
        Assert.Equal([4d, 5d, 6d], Assert.Single(chart.InlineData!.Series).Values);
    }

    [Fact]
    public void UnValoreMancanteResta_un_buco_e_non_diventa_zero_nell_Odp()
    {
        var mapper = new OdfDeckChartMapper();
        var data = new ChartData(["a", "b", "c"], [new ChartSeries("S", [4])]);
        var definition = new ChartDefinition { Id = "c", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty, InlineData = data };
        var document = mapper.BuildDocument(definition, data, new DocumentSize(Twip.FromCentimeters(10), Twip.FromCentimeters(6)), System.Globalization.CultureInfo.InvariantCulture);
        var frame = mapper.BuildFrame(new ObjectElement { Id = "c", Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(2000), new Twip(2000)), Content = definition }, "./Object 1");

        var read = mapper.Read(frame, "c", _ => XDocument.Parse(document.ToString()));

        var values = Assert.Single(Assert.IsType<ChartDefinition>(read!.Content).InlineData!.Series).Values;
        Assert.Equal(4d, values[0]);
        Assert.All(values.Skip(1), value => Assert.True(double.IsNaN(value)));
    }
}
