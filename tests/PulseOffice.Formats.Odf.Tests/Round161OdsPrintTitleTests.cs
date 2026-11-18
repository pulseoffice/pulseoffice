using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round161OdsPrintTitleTests
{
    private static Worksheet RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!.Sheets[0];
    }

    [Fact]
    public void Header_rows_and_columns_in_the_middle_of_the_sheet_keep_their_start()
    {
        var source = Workbook.CreateDefault();
        var sheet = source.Sheets[0];
        sheet.SetValue(new CellAddress(5, 5), CellValue.Number(1));
        sheet.PrintTitleRows = new PrintTitleBand(2, 2);
        sheet.PrintTitleColumns = new PrintTitleBand(1, 3);

        var back = RoundTrip(source);

        Assert.Equal(new PrintTitleBand(2, 2), back.PrintTitleRows);
        Assert.Equal(new PrintTitleBand(1, 3), back.PrintTitleColumns);
        Assert.Equal(1.0, back.GetValue(new CellAddress(5, 5)).AsNumber());
    }

    [Fact]
    public void Header_rows_below_the_last_used_row_and_inside_a_group_survive()
    {
        var source = Workbook.CreateDefault();
        var sheet = source.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.RowGroups.Add(new OutlineGroup { First = 3, Last = 9 });
        sheet.PrintTitleRows = new PrintTitleBand(5, 1);

        var back = RoundTrip(source);

        Assert.Equal(new PrintTitleBand(5, 1), back.PrintTitleRows);
        Assert.Contains(back.RowGroups, group => group.First == 3 && group.Last == 9);
    }
}
