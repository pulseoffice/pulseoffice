using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;
using C = DocumentFormat.OpenXml.Drawing.Charts;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round327StockChartTests
{
    [Fact]
    public void Open_high_low_close_is_written_with_open_and_close_ticks_and_reads_back_plain()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        for (var column = 0; column < 4; column++)
        {
            sheet[new CellAddress(0, column)] = new Cell { Value = CellValue.Text($"S{column}") };
            sheet[new CellAddress(1, column)] = new Cell { Value = CellValue.Number(10 + column) };
        }
        sheet.Objects.Add(new PlacedObject
        {
            Id = "g",
            Anchor = new CellAddress(3, 0),
            Content = new ChartDefinition { Id = "g", Type = ChartType.StockOpenHighLowClose, SourceSheet = "Data", SourceRange = "A1:D2", FirstColumnIsCategory = false },
        });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);

        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var stock = document.WorkbookPart!.WorksheetParts.Single().DrawingsPart!.ChartParts.Single().ChartSpace!.Descendants<C.StockChart>().Single();
            var symbols = stock.Elements<C.LineChartSeries>().Select(series => series.GetFirstChild<C.Marker>()!.GetFirstChild<C.Symbol>()!.Val!.Value).ToList();
            Assert.Equal([C.MarkerStyleValues.Dash, C.MarkerStyleValues.None, C.MarkerStyleValues.None, C.MarkerStyleValues.Dash], symbols);
            Assert.Null(stock.GetFirstChild<C.UpDownBars>());
        }
        stream.Position = 0;
        var read = new XlsxReader().Read(stream);
        var chart = Assert.IsType<ChartDefinition>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
        Assert.Equal(ChartType.StockOpenHighLowClose, chart.Type);
        Assert.Null(chart.SeriesFormats);
    }
}
