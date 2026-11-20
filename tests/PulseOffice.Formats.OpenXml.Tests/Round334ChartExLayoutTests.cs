using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round334ChartExLayoutTests
{
    private static readonly XNamespace Cx = "http://schemas.microsoft.com/office/drawing/2014/chartex";

    private const string ColorStyle = "<cs:colorStyle xmlns:cs=\"http://schemas.microsoft.com/office/drawing/2012/chartStyle\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" meth=\"cycle\" id=\"10\"><a:schemeClr val=\"accent1\"/></cs:colorStyle>";

    private static MemoryStream Written(ChartType type, ChartStatisticalLayout layout, ChartStyleReference? style = null)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        for (var row = 0; row < 4; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text($"V{row}"));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Number(row + 1));
        }
        var chart = new ChartDefinition
        {
            Id = "g", Type = type, SourceSheet = "Dati", SourceRange = "A1:B4", FirstRowIsHeader = false,
            Statistical = layout, Style = style ?? ChartStyleReference.None,
        };
        sheet.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = new CellAddress(0, 3) });
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

    public static TheoryData<ChartType, ChartStatisticalLayout> Layouts => new()
    {
        { ChartType.BoxPlot, new ChartStatisticalLayout { QuartileMethod = ChartQuartileMethod.Inclusive, MeanMarker = false, MeanLine = true, InnerPoints = true, Outliers = false } },
        { ChartType.Histogram, new ChartStatisticalLayout { BinWidth = 2.5, Overflow = 9, IntervalClosedRight = false } },
        { ChartType.Waterfall, new ChartStatisticalLayout { Subtotals = [1, 3] } },
        { ChartType.Treemap, new ChartStatisticalLayout { ParentLabels = ChartParentLabels.Banner } },
    };

    [Theory]
    [MemberData(nameof(Layouts))]
    public void Layout_properties_cross_xlsx_with_and_without_the_classic_fallback(ChartType type, ChartStatisticalLayout layout)
    {
        using var stream = Written(type, layout);
        Assert.Equal(layout, Read(stream).Statistical);

        using (var package = SpreadsheetDocument.Open(stream, true))
        {
            var alternate = package.WorkbookPart!.WorksheetParts.Single().DrawingsPart!.WorksheetDrawing!.ChildElements.Single(element => element.LocalName == "AlternateContent");
            foreach (var fallback in alternate.ChildElements.Where(element => element.LocalName == "Fallback").ToList()) fallback.Remove();
        }
        Assert.Equal(layout, Read(stream).Statistical);
    }

    [Fact]
    public void The_chartex_part_carries_the_model_bin_width()
    {
        using var stream = Written(ChartType.Histogram, new ChartStatisticalLayout { BinWidth = 2.5 });
        using var package = SpreadsheetDocument.Open(stream, false);
        var part = package.WorkbookPart!.WorksheetParts.Single().DrawingsPart!.ExtendedChartParts.Single();
        using var read = part.GetStream();
        Assert.Equal("2.5", (string?)XDocument.Load(read).Descendants(Cx + "binSize").Single().Attribute("val"));
    }

    [Fact]
    public void Chart_style_id_and_colour_style_part_survive_xlsx()
    {
        using var stream = Written(ChartType.Column, ChartStatisticalLayout.Automatic, new ChartStyleReference { StyleId = 26, ColorStyle = ColorStyle });
        var style = Read(stream).Style;
        Assert.Equal(26, style.StyleId);
        Assert.Contains("id=\"10\"", style.ColorStyle, StringComparison.Ordinal);
    }

    [Fact]
    public void Chartex_parent_levels_come_back_as_category_levels()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        string[] parents = ["X", "X", "Y"];
        for (var row = 0; row < 3; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text(parents[row]));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Text($"c{row}"));
            sheet.SetValue(new CellAddress(row, 2), CellValue.Number(row + 1));
        }
        var chart = new ChartDefinition
        {
            Id = "g", Type = ChartType.Treemap, SourceSheet = "Dati", SourceRange = "A1:C3", FirstRowIsHeader = false,
            SeriesSources = [new ChartSeriesSource(null, "Dati!$C$1:$C$3", "Dati!$A$1:$B$3")],
        };
        sheet.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = new CellAddress(0, 4) });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var package = SpreadsheetDocument.Open(stream, true))
        {
            var alternate = package.WorkbookPart!.WorksheetParts.Single().DrawingsPart!.WorksheetDrawing!.ChildElements.Single(element => element.LocalName == "AlternateContent");
            foreach (var fallback in alternate.ChildElements.Where(element => element.LocalName == "Fallback").ToList()) fallback.Remove();
        }

        var read = Read(stream);
        var level = Assert.Single(read.CategoryLevels!);
        Assert.Equal(["X", "", "Y"], level);
    }
}
