using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round195ChartCategoryLevelTests
{
    private static ChartDefinition Levelled() => new()
    {
        Id = "c", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty,
        InlineData = new ChartData(["x", "y", "x"], [new ChartSeries("S", [1, 2, 3])]).WithCategoryLevels([["A", "", "B"]]),
    };

    private static ChartDefinition ThroughPart(ChartDefinition chart, Action<C.ChartSpace>? alter = null)
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

    private static ChartDefinition ThroughOdp(ChartDefinition chart)
    {
        var mapper = new OdfDeckChartMapper();
        var document = mapper.BuildDocument(chart, chart.InlineData!, new DocumentSize(Twip.FromCentimeters(10), Twip.FromCentimeters(6)), System.Globalization.CultureInfo.InvariantCulture);
        var frame = mapper.BuildFrame(new ObjectElement { Id = "c", Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(2000), new Twip(2000)), Content = chart }, "./Object 1");
        return Assert.IsType<ChartDefinition>(mapper.Read(frame, "c", _ => XDocument.Parse(document.ToString()))!.Content);
    }

    [Fact]
    public void Inline_category_levels_cross_the_ooxml_literal()
    {
        var data = ThroughPart(Levelled()).InlineData!;

        Assert.Equal(["x", "y", "x"], data.Categories);
        Assert.Equal(["A", "", "B"], Assert.Single(data.CategoryLevels!));
    }

    [Fact]
    public void A_multi_level_cache_reads_leaves_and_outer_labels()
    {
        var data = ThroughPart(Levelled() with { InlineData = new ChartData(["?"], [new ChartSeries("S", [1, 2, 3])]) }, space =>
        {
            var axis = space.Descendants<C.CategoryAxisData>().Single();
            axis.RemoveAllChildren();
            var cache = new C.MultiLevelStringCache(new C.PointCount { Val = 3u },
                new C.Level(Point(0, "x"), Point(1, "y"), Point(2, "x")),
                new C.Level(Point(0, "A"), Point(2, "B")));
            axis.Append(new C.MultiLevelStringReference(new C.Formula("Sheet1!$A$2:$B$4"), cache));
        }).InlineData!;

        Assert.Equal(["x", "y", "x"], data.Categories);
        Assert.Equal(["A", "", "B"], Assert.Single(data.CategoryLevels!));
    }

    [Fact]
    public void Inline_category_levels_and_hidden_columns_cross_the_odf_local_table()
    {
        var chart = Levelled() with
        {
            InlineData = Levelled().InlineData! with { HiddenColumns = [new ChartHiddenColumn(2, [0.1, 0.2, 0.3])] },
            SeriesFormats = [new ChartSeriesFormat { ErrorBars = new ChartErrorBars(ErrorBarKind.Custom, 0) { Plus = new ChartErrorValues("local-table!C2:C4", [9, 9, 9]) } }],
        };

        var read = ThroughOdp(chart);

        Assert.Equal(["x", "y", "x"], read.InlineData!.Categories);
        Assert.Single(read.InlineData.Series);
        Assert.Equal(["A", "", "B"], Assert.Single(read.InlineData.CategoryLevels!));
        Assert.Equal([0.1, 0.2, 0.3], read.InlineData.HiddenColumn(2)!.Values);
        Assert.Equal([0.1, 0.2, 0.3], read.SeriesFormats![0]!.ErrorBars!.Plus!.Values);
    }

    private static C.StringPoint Point(uint index, string text) => new(new C.NumericValue(text)) { Index = index };
}
