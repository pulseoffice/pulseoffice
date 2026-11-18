using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round161XlsxPrintTitleTests
{
    [Fact]
    public void Print_titles_that_do_not_start_at_the_first_line_keep_their_start()
    {
        var source = Workbook.CreateDefault();
        var sheet = source.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.PrintTitleRows = new PrintTitleBand(2, 2);
        sheet.PrintTitleColumns = new PrintTitleBand(1, 3);

        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;
        var result = new XlsxReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));

        var back = result.Value!.Sheets[0];
        Assert.Equal(new PrintTitleBand(2, 2), back.PrintTitleRows);
        Assert.Equal(new PrintTitleBand(1, 3), back.PrintTitleColumns);
    }
}
