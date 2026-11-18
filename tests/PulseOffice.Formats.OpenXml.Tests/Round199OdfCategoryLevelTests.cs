using System.Xml.Linq;
using PulseOffice.Charts;
using PulseOffice.Formats.Odf;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round199OdfCategoryLevelTests
{
    private static readonly XNamespace Table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
    private static readonly XNamespace Chart = "urn:oasis:names:tc:opendocument:xmlns:chart:1.0";
    private static readonly XNamespace Office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private static readonly XNamespace Text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private static readonly XNamespace Pulse = "urn:pulseoffice:chart";

    private readonly OdfDeckChartMapper _mapper = new();

    private static ChartDefinition Inline(ChartData data) => new()
    {
        Id = "c", Type = ChartType.Column, SourceSheet = string.Empty, SourceRange = string.Empty, InlineData = data,
    };

    private XDocument Document(ChartDefinition chart) =>
        _mapper.BuildDocument(chart, chart.InlineData!, new DocumentSize(Twip.FromCentimeters(10), Twip.FromCentimeters(6)), System.Globalization.CultureInfo.InvariantCulture);

    private ChartDefinition Read(ChartDefinition chart, XDocument document)
    {
        var frame = _mapper.BuildFrame(new ObjectElement { Id = "c", Bounds = new DocumentRect(new Twip(0), new Twip(0), new Twip(2000), new Twip(2000)), Content = chart }, "./Object 1");
        return Assert.IsType<ChartDefinition>(_mapper.Read(frame, "c", _ => XDocument.Parse(document.ToString()))!.Content);
    }

    [Fact]
    public void Category_levels_are_label_columns_before_the_leaves_named_by_the_categories_range()
    {
        var chart = Inline(new ChartData(["x", "y", "x"], [new ChartSeries("S", [1, 2, 3])]).WithCategoryLevels([["A", "", "B"]]));

        var document = Document(chart);

        var categories = document.Descendants(Chart + "categories").Single();
        Assert.Equal("local-table.A2:.B4", (string?)categories.Attribute(Table + "cell-range-address"));
        Assert.Equal("local-table.C2:.C4", (string?)document.Descendants(Chart + "series").Single().Attribute(Chart + "values-cell-range-address"));
        var table = document.Descendants(Table + "table").Single();
        Assert.DoesNotContain(table.Attributes(), attribute => attribute.Name.Namespace == Pulse);
        var first = table.Element(Table + "table-rows")!.Elements(Table + "table-row").First().Elements(Table + "table-cell").Select(cell => cell.Value).ToList();
        Assert.Equal(["A", "x", "1"], first);
        var read = Read(chart, document).InlineData!;
        Assert.Equal(["x", "y", "x"], read.Categories);
        Assert.Equal(["A", "", "B"], Assert.Single(read.CategoryLevels!));
        Assert.Equal([1d, 2d, 3d], Assert.Single(read.Series).Values);
    }

    [Fact]
    public void Level_columns_named_by_the_old_extension_are_still_read()
    {
        var chart = Inline(new ChartData(["x", "y"], [new ChartSeries("S", [1, 2])]));
        var document = Document(chart);
        var table = document.Descendants(Table + "table").Single();
        table.Add(new XAttribute(Pulse + "category-level-column", "2"), new XAttribute(Pulse + "category-level-count", "1"));
        var labels = new[] { "A", "B" };
        var rows = table.Element(Table + "table-rows")!.Elements(Table + "table-row").ToList();
        for (var row = 0; row < rows.Count; row++)
            rows[row].Add(new XElement(Table + "table-cell", new XAttribute(Office + "value-type", "string"), new XElement(Text + "p", labels[row])));

        var read = Read(chart, document).InlineData!;

        Assert.Equal(["x", "y"], read.Categories);
        Assert.Equal(["A", "B"], Assert.Single(read.CategoryLevels!));
        Assert.Single(read.Series);
    }
}
