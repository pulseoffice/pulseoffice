using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round330ChartExWritingTests
{
    private static readonly XNamespace Cx = "http://schemas.microsoft.com/office/drawing/2014/chartex";

    private static CellAddress At(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static MemoryStream Written(ChartType type)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet[At("A1")] = new Cell { Value = CellValue.Text("Voce") };
        sheet[At("B1")] = new Cell { Value = CellValue.Text("Uno") };
        for (var row = 0; row < 3; row++)
        {
            sheet[At($"A{row + 2}")] = new Cell { Value = CellValue.Text($"V{row + 1}") };
            sheet[At($"B{row + 2}")] = new Cell { Value = CellValue.Number(10 + row) };
        }
        var chart = new ChartDefinition { Id = "grafico1", Type = type, SourceSheet = "Dati", SourceRange = "A1:B4", ChartTitle = "Quote" };
        sheet.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = At("E2") });
        var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return stream;
    }

    private static ChartDefinition Read(Stream stream)
    {
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return Assert.IsType<ChartDefinition>(Assert.Single(result.Value!.Sheets[0].Objects).Content);
    }

    [Theory]
    [InlineData(ChartType.Treemap, "treemap")]
    [InlineData(ChartType.Pareto, "paretoLine")]
    [InlineData(ChartType.Funnel, "funnel")]
    public void Statistical_and_hierarchical_charts_get_a_native_chartex_part_and_round_trip(ChartType type, string layout)
    {
        using var stream = Written(type);
        using (var package = SpreadsheetDocument.Open(stream, false))
        {
            var drawings = package.WorkbookPart!.WorksheetParts.Single().DrawingsPart!;
            var part = Assert.Single(drawings.ExtendedChartParts);
            XDocument space;
            using (var read = part.GetStream()) space = XDocument.Load(read);
            Assert.Contains(space.Descendants(Cx + "series"), series => (string?)series.Attribute("layoutId") == layout);
            Assert.Contains(space.Descendants(Cx + "f"), formula => formula.Value.Contains("$B$2:$B$4", StringComparison.Ordinal));
            Assert.Contains(drawings.WorksheetDrawing!.ChildElements, element => element.LocalName == "AlternateContent");
        }

        Assert.Equal(type, Read(stream).Type);
    }

    [Fact]
    public void The_chartex_part_alone_still_reads_the_type()
    {
        using var stream = Written(ChartType.Treemap);
        using (var package = SpreadsheetDocument.Open(stream, true))
        {
            var alternate = package.WorkbookPart!.WorksheetParts.Single().DrawingsPart!.WorksheetDrawing!.ChildElements.Single(element => element.LocalName == "AlternateContent");
            foreach (var fallback in alternate.ChildElements.Where(element => element.LocalName == "Fallback").ToList()) fallback.Remove();
        }

        var chart = Read(stream);
        Assert.Equal(ChartType.Treemap, chart.Type);
        Assert.Equal("Quote", chart.ChartTitle);
    }

    [Fact]
    public void Native_column_charts_keep_the_classic_anchor()
    {
        using var stream = Written(ChartType.Column);
        using var package = SpreadsheetDocument.Open(stream, false);
        var drawings = package.WorkbookPart!.WorksheetParts.Single().DrawingsPart!;
        Assert.Empty(drawings.ExtendedChartParts);
        Assert.DoesNotContain(drawings.WorksheetDrawing!.ChildElements, element => element.LocalName == "AlternateContent");
    }
}
