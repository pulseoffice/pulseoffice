using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round120FilterDateTests
{
    private static Workbook DatedBook()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        var dateStyle = workbook.Styles.Derive(0, style => style with { NumberFormatId = PulseOffice.Sheets.Formatting.NumberFormatCatalog.SystemShortDateId });
        sheet.SetValue(CellAddress.First, CellValue.Text("When"));
        DateTime[] days = [new(2026, 3, 1), new(2026, 3, 15), new(2025, 1, 1)];
        for (var index = 0; index < days.Length; index++)
        {
            var address = new CellAddress(index + 1, 0);
            sheet.SetValue(address, CellValue.Number(days[index].ToOADate()));
            sheet.SetStyle(address, dateStyle);
        }
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(3, 0)));
        sheet.AutoFilter.Set(new ColumnFilter(0)
        {
            AllowedValues = new HashSet<string>([CellValue.Number(days[0].ToOADate()).ToString(), CellValue.Number(days[1].ToOADate()).ToString()]),
            IncludeBlanks = false,
        });
        return workbook;
    }

    [Fact]
    public void Allowed_dates_are_written_to_xlsx_as_date_group_items_and_read_back()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(DatedBook(), stream);
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var filters = document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<Filters>().Single();
            Assert.Empty(filters.Elements<Filter>());
            var items = filters.Elements<DateGroupItem>().ToList();
            Assert.Equal([(2026, 3, 1), (2026, 3, 15)], items.Select(item => ((int)item.Year!.Value, (int)item.Month!.Value, (int)item.Day!.Value)));
            Assert.All(items, item => Assert.Equal(DateTimeGroupingValues.Day, item.DateTimeGrouping!.Value));
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!.Sheets[0];
        Assert.Equal([3], new SheetFilterService().RowsToHide(read, read.AutoFilter.Range!.Value, read.AutoFilter.Filters));
    }

    [Fact]
    public void Allowed_dates_are_written_to_ods_as_iso_dates_and_read_back()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(DatedBook(), stream);
        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            using var content = archive.GetEntry("content.xml")!.Open();
            XNamespace table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
            var items = XDocument.Load(content).Descendants(table + "filter-set-item").Select(item => (string?)item.Attribute(table + "value")).ToList();
            Assert.Equal(["2026-03-01", "2026-03-15"], items);
        }

        stream.Position = 0;
        var read = new OdsReader().Read(stream).Value!.Sheets[0];
        Assert.Equal([3], new SheetFilterService().RowsToHide(read, read.AutoFilter.Range!.Value, read.AutoFilter.Filters));
    }
}
