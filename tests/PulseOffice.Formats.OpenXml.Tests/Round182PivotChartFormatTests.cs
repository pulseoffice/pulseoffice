using PulseOffice.Charts;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round182PivotChartFormatTests
{
    private static Workbook Sample(ChartDefinition chart)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Pivot");
        sheet[new CellAddress(0, 0)] = new Cell { Value = CellValue.Text("Region") };
        sheet[new CellAddress(0, 1)] = new Cell { Value = CellValue.Text("Amount") };
        sheet[new CellAddress(1, 0)] = new Cell { Value = CellValue.Text("A") };
        sheet[new CellAddress(1, 1)] = new Cell { Value = CellValue.Number(3) };
        sheet[new CellAddress(2, 0)] = new Cell { Value = CellValue.Text("B") };
        sheet[new CellAddress(2, 1)] = new Cell { Value = CellValue.Number(5) };
        sheet.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = new CellAddress(0, 4) });
        return workbook;
    }

    private static ChartDefinition Linked() => new()
    {
        Id = "pc",
        Type = ChartType.Column,
        SourceSheet = "Pivot",
        SourceRange = "A1:B3",
        PivotSource = new ChartPivotSource("Sales") { FormatId = 2, ShowFieldButtons = false },
    };

    private static ChartDefinition Static() => new()
    {
        Id = "st",
        Type = ChartType.Column,
        SourceSheet = string.Empty,
        SourceRange = string.Empty,
        InlineData = new ChartData(["A", "B"], [new ChartSeries("Amount", [3, 5])]),
    };

    private static Workbook Xlsx(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static Workbook Ods(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }

    private static ChartDefinition ChartOf(Workbook workbook) =>
        Assert.IsType<ChartDefinition>(Assert.Single(workbook.FindSheet("Pivot")!.Objects).Content);

    [Fact]
    public void The_pivot_link_crosses_xlsx()
    {
        var read = ChartOf(Xlsx(Sample(Linked())));

        Assert.Equal(new ChartPivotSource("Sales") { FormatId = 2, ShowFieldButtons = false }, read.PivotSource);
    }

    [Fact]
    public void The_pivot_link_crosses_ods()
    {
        var read = ChartOf(Ods(Sample(Linked() with { PivotSource = new ChartPivotSource("Sales") { FormatId = 1 } })));

        Assert.Equal(new ChartPivotSource("Sales") { FormatId = 1 }, read.PivotSource);
    }

    [Fact]
    public void A_chart_left_by_a_deleted_pivot_keeps_its_values_in_both_formats()
    {
        foreach (var read in new[] { ChartOf(Xlsx(Sample(Static()))), ChartOf(Ods(Sample(Static()))) })
        {
            var data = new ChartDataExtractor().Extract(new Workbook(), read);
            Assert.Null(read.PivotSource);
            Assert.Equal(["A", "B"], data.Categories);
            Assert.Equal([3d, 5d], Assert.Single(data.Series).Values);
        }
    }
}
