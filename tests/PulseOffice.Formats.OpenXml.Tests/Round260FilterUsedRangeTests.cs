using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round260FilterUsedRangeTests
{
    [Fact]
    public void A_whole_column_date_group_filter_reads_only_the_used_rows_and_keeps_its_matches()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("When"));
        DateTime[] days = [new(2026, 3, 1), new(2025, 1, 1)];
        var dateStyle = workbook.Styles.Derive(0, style => style with { NumberFormatId = PulseOffice.Sheets.Formatting.NumberFormatCatalog.SystemShortDateId });
        for (var index = 0; index < days.Length; index++)
        {
            sheet.SetValue(new CellAddress(index + 1, 0), CellValue.Number(days[index].ToOADate()));
            sheet.SetStyle(new CellAddress(index + 1, 0), dateStyle);
        }
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(2, 0)));
        sheet.AutoFilter.Set(new ColumnFilter(0) { AllowedValues = new HashSet<string>([CellValue.Number(days[0].ToOADate()).ToString()]), IncludeBlanks = false });
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            Assert.NotEmpty(document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<DocumentFormat.OpenXml.Spreadsheet.DateGroupItem>());
            var part = document.WorkbookPart!.WorksheetParts.First();
            part.Worksheet!.Descendants<DocumentFormat.OpenXml.Spreadsheet.AutoFilter>().Single().Reference = "A1:A1048576";
            part.Worksheet.Save();
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!.Sheets[0];

        Assert.Single(read.AutoFilter.Filters.Single().AllowedValues!);
        Assert.Equal([2], new SheetFilterService().RowsToHide(read, new CellRange(CellAddress.First, new CellAddress(2, 0)), read.AutoFilter.Filters));
    }
}
