using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class TopItemsAutoFilterTests
{
    [Theory]
    [InlineData("ods", false, false)]
    [InlineData("ods", true, true)]
    [InlineData("xlsx", false, false)]
    [InlineData("xlsx", true, true)]
    public void IlFiltroDeiPrimiElementiFaIlGiroNeiDueFormati(string format, bool bottom, bool percent)
    {
        var criterion = new TopItemsCriterion(bottom, percent, 4);
        var read = Through(format, Workbook(criterion));

        var filter = Assert.Single(read.Sheets[0].AutoFilter.Filters);
        Assert.Equal(criterion, filter.Top);
    }

    [Fact]
    public void IlFiltroDiXlsxPassaInOdsSenzaPerdersi()
    {
        var criterion = new TopItemsCriterion(false, false, 3);
        var read = Through("ods", Through("xlsx", Workbook(criterion)));

        Assert.Equal(criterion, Assert.Single(read.Sheets[0].AutoFilter.Filters).Top);
    }

    private Workbook Workbook(TopItemsCriterion criterion)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Valori");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Numero"));
        for (var row = 1; row <= 10; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        sheet.AutoFilter.Activate(new CellRange(new CellAddress(0, 0), new CellAddress(10, 0)));
        sheet.AutoFilter.Set(new ColumnFilter(0) { Top = criterion });
        return workbook;
    }

    private Workbook Through(string format, Workbook workbook)
    {
        using var stream = new MemoryStream();
        if (format == "ods") new OdsWriter().Write(workbook, stream); else new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return (format == "ods" ? new OdsReader().Read(stream) : new XlsxReader().Read(stream)).Value!;
    }
}
