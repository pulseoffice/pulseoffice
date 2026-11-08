using System.Diagnostics;
using System.Text;
using PulseOffice.Foundation.Text;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Interchange.Tests;

public sealed class CsvWholeColumnRound21Tests
{
    [Fact]
    public void Exporting_a_sheet_with_whole_column_formats_writes_only_the_used_rows()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.Layout.SetColumnStyles(0, 2, workbook.Styles.Intern(PulseOffice.Sheets.Styling.CellStyle.Default with { BackgroundHex = "#00FF00" }));
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.SetValue(new CellAddress(2, 1), CellValue.Text("x"));

        using var stream = new MemoryStream();
        var clock = Stopwatch.StartNew();
        new CsvSpreadsheetWriter(new DelimitedText()).Write(workbook, stream);
        clock.Stop();

        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(400), clock.Elapsed.ToString());
        Assert.Equal(3, Encoding.UTF8.GetString(stream.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }
}
