using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round323ChartLeftoverTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static Workbook Book(ChartDefinition chart)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet[A("A1")] = new Cell { Value = CellValue.Text("Mese") };
        sheet[A("B1")] = new Cell { Value = CellValue.Text("Uno") };
        sheet[A("C1")] = new Cell { Value = CellValue.Text("Due") };
        sheet[A("D1")] = new Cell { Value = CellValue.Text("Tre") };
        for (var row = 0; row < 3; row++)
        {
            sheet[A($"A{row + 2}")] = new Cell { Value = CellValue.Text($"M{row + 1}") };
            sheet[A($"B{row + 2}")] = new Cell { Value = CellValue.Number(10 + row) };
            sheet[A($"C{row + 2}")] = new Cell { Value = CellValue.Number(20 + row) };
            sheet[A($"D{row + 2}")] = new Cell { Value = CellValue.Number(30 + row) };
        }
        sheet.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = A("F2") });
        return workbook;
    }

    private static ChartDefinition Chart(ChartType type, string range = "A1:C4") =>
        new() { Id = "grafico1", Type = type, SourceSheet = "Dati", SourceRange = range };

    private static ChartDefinition RoundTrip(ChartDefinition chart, string format, Action<SpreadsheetDocument>? alter = null)
    {
        using var stream = new MemoryStream();
        if (format == "xlsx") new XlsxWriter().Write(Book(chart), stream);
        else new OdsWriter().Write(Book(chart), stream);
        if (alter is not null)
        {
            using var package = SpreadsheetDocument.Open(stream, true);
            alter(package);
        }
        stream.Position = 0;
        var result = format == "xlsx" ? new XlsxReader().Read(stream) : new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return Assert.IsType<ChartDefinition>(Assert.Single(result.Value!.Sheets[0].Objects).Content);
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("ods")]
    public void A_combination_whose_line_series_comes_first_keeps_that_series_as_the_line(string format)
    {
        var chart = Chart(ChartType.ColumnLine, "A1:D4") with { LineSeriesIndices = [0] };

        var read = RoundTrip(chart, format);

        Assert.Equal(ChartType.ColumnLine, read.Type);
        if (format == "ods")
        {
            Assert.True(read.IsLineSeries(0, 3));
            Assert.False(read.IsLineSeries(2, 3));
        }
        else
        {
            Assert.Equal(1, Enumerable.Range(0, 3).Count(series => read.IsLineSeries(series, 3)));
        }
    }

    [Fact]
    public void Line_series_indices_choose_the_line_series_in_the_model()
    {
        var chart = Chart(ChartType.ColumnLine) with { LineSeriesIndices = [1] };

        Assert.Equal([false, true, false], Enumerable.Range(0, 3).Select(series => chart.IsLineSeries(series, 3)));
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("ods")]
    public void Best_fit_data_labels_cross_both_formats(string format)
    {
        var chart = Chart(ChartType.Pie, "A1:B4") with
        {
            ShowDataLabels = true,
            DataLabels = new ChartDataLabelOptions { ShowValue = true, ShowPercent = true, Placement = DataLabelPlacement.BestFit },
        };

        var read = RoundTrip(chart, format);

        Assert.Equal(DataLabelPlacement.BestFit, read.DataLabels.Placement);
    }

    [Fact]
    public void A_stock_series_keeps_its_colour_in_xlsx()
    {
        var chart = Chart(ChartType.StockHighLowClose, "A1:D4") with { SeriesColors = ["#112233", null, "#445566"] };

        var read = RoundTrip(chart, "xlsx");

        Assert.Equal("#112233", read.ExplicitSeriesColor(0));
        Assert.Equal("#445566", read.ExplicitSeriesColor(2));
    }
}
