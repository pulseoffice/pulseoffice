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
        var lines = 0;
        new PulseOffice.Testing.GrowthCheck().Flat(rows =>
        {
            var workbook = Workbook.CreateDefault();
            var sheet = workbook.Sheets[0];
            var style = workbook.Styles.Intern(PulseOffice.Sheets.Styling.CellStyle.Default with { BackgroundHex = "#00FF00" });
            if (rows == CellAddress.MaxRows) sheet.Layout.SetColumnStyles(0, 2, style);
            else sheet.Layout.SetStyleBlock(new CellRange(new CellAddress(0, 0), new CellAddress(rows - 1, 2)), style);
            sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
            sheet.SetValue(new CellAddress(2, 1), CellValue.Text("x"));
            return () =>
            {
                using var stream = new MemoryStream();
                new CsvSpreadsheetWriter(new DelimitedText()).Write(workbook, stream);
                lines = Encoding.UTF8.GetString(stream.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            };
        }, 3, CellAddress.MaxRows);

        Assert.Equal(3, lines);
    }
}
