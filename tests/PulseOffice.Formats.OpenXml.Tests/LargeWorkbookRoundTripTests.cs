using System.Diagnostics;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;
using Book = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class LargeWorkbookRoundTripTests(ITestOutputHelper output)
{
    private const int Columns = 20;

    private static Book Cells(int rows)
    {
        var workbook = Book.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < rows; row++)
            for (var column = 0; column < Columns; column++)
                sheet.SetValue(new CellAddress(row, column), column % 4 == 0 ? CellValue.Text("t" + (row % 1000)) : CellValue.Number(row * 0.5 + column));
        return workbook;
    }

    private TimeSpan Crossing(int rows)
    {
        var workbook = Cells(rows);
        var clock = Stopwatch.StartNew();
        using var xlsx = new MemoryStream();
        new XlsxWriter().Write(workbook, xlsx);
        var xlsxWrite = clock.Elapsed;
        xlsx.Position = 0;
        clock.Restart();
        var fromXlsx = new XlsxReader().Read(xlsx).Require();
        var xlsxRead = clock.Elapsed;
        clock.Restart();
        using var ods = new MemoryStream();
        new OdsWriter().Write(fromXlsx, ods);
        var odsWrite = clock.Elapsed;
        ods.Position = 0;
        clock.Restart();
        var fromOds = new OdsReader().Read(ods).Require();
        var odsRead = clock.Elapsed;
        output.WriteLine($"rows {rows}: xlsx write {xlsxWrite.TotalMilliseconds:F0} ms read {xlsxRead.TotalMilliseconds:F0} ms ({xlsx.Length / 1024} KB); ods write {odsWrite.TotalMilliseconds:F0} ms read {odsRead.TotalMilliseconds:F0} ms ({ods.Length / 1024} KB)");

        var last = new CellAddress(rows - 1, Columns - 1);
        Assert.Equal(workbook.Sheets[0].GetValue(last), fromOds.Sheets[0].GetValue(last));
        Assert.Equal(rows * Columns, fromOds.Sheets[0].NonEmptyCells().Count());
        return xlsxWrite + xlsxRead + odsWrite + odsRead;
    }

    [Fact]
    public void Many_cells_cross_xlsx_and_ods_in_linear_time()
    {
        Crossing(500);
        var small = Crossing(500);
        var large = Crossing(5_000);

        Assert.True(large < 40 * TimeSpan.FromMilliseconds(Math.Max(small.TotalMilliseconds, 25)), $"{large.TotalMilliseconds:F0} ms against {small.TotalMilliseconds:F0} ms for a tenth of the cells");
    }
}
