using PulseOffice.Charts;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round172ChartErrorTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private static string Reference(string range)
    {
        Assert.True(CellRange.TryParseA1(range, out var parsed));
        return new ChartSourceReferences().Write("Dati", parsed);
    }

    private static Workbook Book(ChartDefinition chart)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet[A("A1")] = new Cell { Value = CellValue.Text("X") };
        sheet[A("B1")] = new Cell { Value = CellValue.Text("Y") };
        for (var row = 0; row < 3; row++)
        {
            sheet[A($"A{row + 2}")] = new Cell { Value = CellValue.Number(row + 1) };
            sheet[A($"B{row + 2}")] = new Cell { Value = CellValue.Number(10 + row) };
            sheet[A($"C{row + 2}")] = new Cell { Value = CellValue.Number(row + 0.5) };
            sheet[A($"D{row + 2}")] = new Cell { Value = CellValue.Number(row + 0.25) };
        }
        sheet.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = A("F2") });
        return workbook;
    }

    private static ChartDefinition Chart(ChartType type, ChartSeriesFormat format) =>
        new ChartDefinition { Id = "grafico1", Type = type, SourceSheet = "Dati", SourceRange = "A1:B4" }.WithSeriesFormat(0, format);

    private static ChartDefinition RoundTrip(ChartDefinition chart, string format)
    {
        using var stream = new MemoryStream();
        if (format == "xlsx") new XlsxWriter().Write(Book(chart), stream);
        else new OdsWriter().Write(Book(chart), stream);
        stream.Position = 0;
        var result = format == "xlsx" ? new XlsxReader().Read(stream) : new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return Assert.IsType<ChartDefinition>(Assert.Single(result.Value!.Sheets[0].Objects).Content);
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("ods")]
    public void Error_values_taken_from_cells_cross_the_format(string format)
    {
        var bars = new ChartErrorBars(ErrorBarKind.Custom, 1)
        {
            Plus = new ChartErrorValues(Reference("C2:C4"), [0.5, 1.5, 2.5]),
            Minus = new ChartErrorValues(Reference("D2:D4"), [0.25, 1.25, 2.25]),
        };

        var read = RoundTrip(Chart(ChartType.Line, new ChartSeriesFormat { ErrorBars = bars }), format).SeriesFormat(0).ErrorBars;

        Assert.NotNull(read);
        Assert.Equal(ErrorBarKind.Custom, read.Kind);
        Assert.Equal(bars.Plus, read.Plus);
        Assert.Equal(bars.Minus, read.Minus);
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("ods")]
    public void Horizontal_error_bars_of_a_scatter_series_cross_the_format(string format)
    {
        var across = new ChartErrorBars(ErrorBarKind.FixedValue, 0.5) { Direction = ErrorBarDirection.Minus };

        var read = RoundTrip(Chart(ChartType.Scatter, new ChartSeriesFormat { XErrorBars = across }), format).SeriesFormat(0);

        Assert.Equal(across, read.XErrorBars);
        Assert.Null(read.ErrorBars);
    }

    [Fact]
    public void Literal_error_values_are_declared_in_ods()
    {
        var bars = new ChartErrorBars(ErrorBarKind.Custom, 1) { Plus = new ChartErrorValues(null, [1, 2, 3]), Minus = new ChartErrorValues(null, [1, 2, 3]) };

        var losses = new OdsSpreadsheetWriter().CheckFidelity(Book(Chart(ChartType.Line, new ChartSeriesFormat { ErrorBars = bars }))).Losses;

        Assert.Contains(losses, loss => loss.Code == "ODF287");
    }

    [Fact]
    public void Custom_spans_use_each_point_own_values()
    {
        var bars = new ChartErrorBars(ErrorBarKind.Custom, 1) { Plus = new ChartErrorValues(null, [1, 2]), Minus = new ChartErrorValues(null, [3]) };

        var spans = new ChartErrorSpans().Spans(bars, [10, 20]);

        Assert.Equal(new ChartErrorSpan(10, 7, 11), spans[0]);
        Assert.Equal(new ChartErrorSpan(20, 20, 22), spans[1]);
    }
}
