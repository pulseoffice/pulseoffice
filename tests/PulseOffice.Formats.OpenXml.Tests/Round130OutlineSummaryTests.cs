using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using Xunit;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round130OutlineSummaryTests
{
    private static Workbook Book(OutlineSummary summary)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new Sheets.Addressing.CellAddress(5, 0), Sheets.Values.CellValue.Number(1));
        sheet.RowGroups.Add(new OutlineGroup { First = 2, Last = 4 });
        sheet.OutlineSummary = summary;
        return workbook;
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Summary_position_crosses_xlsx(bool below, bool right)
    {
        var read = ThroughXlsx(Book(new OutlineSummary(below, right)));

        Assert.Equal(new OutlineSummary(below, right), read.Sheets[0].OutlineSummary);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Summary_position_crosses_ods(bool below, bool right)
    {
        var read = ThroughOds(Book(new OutlineSummary(below, right)));

        Assert.Equal(new OutlineSummary(below, right), read.Sheets[0].OutlineSummary);
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
