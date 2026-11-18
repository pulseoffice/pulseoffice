using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round206LeftToRightSortTests
{
    private static readonly SortSettings Rightward = new()
    {
        Fields = [new SortField(2, false)],
        Orientation = SortOrientation.LeftToRight,
    };

    private static Workbook Book()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 4; row++)
            for (var column = 0; column < 2; column++)
                sheet.SetValue(new CellAddress(row, column), CellValue.Number(row + column));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(3, 1)));
        sheet.AutoFilter.Remember(Rightward);
        return workbook;
    }

    [Fact]
    public void A_rightward_sort_whose_key_row_lies_beyond_the_column_count_is_remembered()
    {
        Assert.Equal(Rightward, Book().Sheets[0].AutoFilter.Sort);
    }

    [Fact]
    public void A_rightward_autofilter_sort_crosses_both_formats()
    {
        foreach (var read in new[] { ThroughXlsx(Book()), ThroughOds(Book()) })
        {
            var sort = read.Sheets[0].AutoFilter.Sort;
            Assert.NotNull(sort);
            Assert.True(sort.LeftToRight);
            Assert.Equal([new SortField(2, false)], sort.Fields);
        }
    }

    private static Workbook ThroughXlsx(Workbook source)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(source, stream);
        stream.Position = 0;
        return new XlsxReader().Read(stream).Value!;
    }

    private static Workbook ThroughOds(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;
        return new OdsReader().Read(stream).Value!;
    }
}
