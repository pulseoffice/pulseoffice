using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class ColorAutoFilterTests
{
    [Theory]
    [InlineData("ods")]
    [InlineData("xlsx")]
    public void IFiltriPerColoreFannoIlGiroNeiDueFormati(string format)
    {
        var read = Through(format, Workbook());
        var filters = read.Sheets[0].AutoFilter.Filters;

        Assert.Equal(ColorFilterTarget.Background, filters.Single(filter => filter.ColumnOffset == 0).Color!.Target);
        Assert.Equal("#E8F2A1", filters.Single(filter => filter.ColumnOffset == 0).Color!.ColorHex!.ToUpperInvariant());
        Assert.Equal("#3465A4", filters.Single(filter => filter.ColumnOffset == 1).Color!.ColorHex!.ToUpperInvariant());
        Assert.Equal(ColorFilterTarget.Text, filters.Single(filter => filter.ColumnOffset == 1).Color!.Target);
        Assert.All(filters, filter => Assert.Null(filter.Criteria));
    }

    private Workbook Workbook()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Colori");
        var yellow = workbook.Styles.Intern(CellStyle.Default with { BackgroundHex = "#E8F2A1" });
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Sfondo"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Testo"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(1));
        sheet.SetStyle(new CellAddress(1, 0), yellow);
        sheet.SetValue(new CellAddress(2, 0), CellValue.Number(2));
        sheet.AutoFilter.Activate(new CellRange(new CellAddress(0, 0), new CellAddress(2, 1)));
        sheet.AutoFilter.Set(new ColumnFilter(0) { Color = new ColorFilterCriterion(ColorFilterTarget.Background, "#E8F2A1") });
        sheet.AutoFilter.Set(new ColumnFilter(1) { Color = new ColorFilterCriterion(ColorFilterTarget.Text, "#3465A4") });
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
