using System.Diagnostics;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;
using Book = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class LargeWorkbookRoundTripTests(ITestOutputHelper output)
{
    private const int Rows = 50_000;
    private const int Columns = 20;

    private static Book Million()
    {
        var workbook = Book.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < Rows; row++)
            for (var column = 0; column < Columns; column++)
                sheet.SetValue(new CellAddress(row, column), column % 4 == 0 ? CellValue.Text("t" + (row % 1000)) : CellValue.Number(row * 0.5 + column));
        return workbook;
    }

    [Fact]
    public void A_million_cells_cross_xlsx_and_ods_in_bounded_time()
    {
        var workbook = Million();
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
        output.WriteLine($"xlsx write {xlsxWrite.TotalSeconds:F1}s read {xlsxRead.TotalSeconds:F1}s ({xlsx.Length / 1024} KB); ods write {odsWrite.TotalSeconds:F1}s read {odsRead.TotalSeconds:F1}s ({ods.Length / 1024} KB); managed {GC.GetTotalMemory(false) / 1048576} MB working set {Environment.WorkingSet / 1048576} MB");

        var last = new CellAddress(Rows - 1, Columns - 1);
        Assert.Equal(workbook.Sheets[0].GetValue(last), fromOds.Sheets[0].GetValue(last));
        Assert.Equal(Rows * Columns, fromOds.Sheets[0].NonEmptyCells().Count());
        Assert.True(xlsxWrite + xlsxRead + odsWrite + odsRead < TimeSpan.FromMinutes(2));
    }
}
