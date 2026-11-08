using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round117FilterTests
{
    private readonly double _march1 = new DateTime(2026, 3, 1).ToOADate();
    private readonly double _march15 = new DateTime(2026, 3, 15).ToOADate();
    private readonly double _january = new DateTime(2025, 1, 1).ToOADate();

    [Fact]
    public void Date_group_items_written_by_spreadsheet_applications_are_read_as_the_matching_values()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("When"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(_march1));
        sheet.SetValue(new CellAddress(2, 0), CellValue.Number(_march15));
        sheet.SetValue(new CellAddress(3, 0), CellValue.Number(_january));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(3, 0)));
        sheet.AutoFilter.Set(new ColumnFilter(0) { AllowedValues = new HashSet<string>(["1"]), IncludeBlanks = false });

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var filters = document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<Filters>().Single();
            filters.RemoveAllChildren();
            filters.Append(new DateGroupItem { Year = 2026, Month = 3, DateTimeGrouping = DateTimeGroupingValues.Month });
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!;
        var filtered = read.Sheets[0];
        var hidden = new SheetFilterService().RowsToHide(filtered, filtered.AutoFilter.Range!.Value, filtered.AutoFilter.Filters);

        Assert.Equal([3], hidden);
    }

    [Fact]
    public void Value_filters_stay_case_insensitive_through_both_formats()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(CellAddress.First, CellValue.Text("City"));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Text("Roma"));
        sheet.SetValue(new CellAddress(2, 0), CellValue.Text("ROMA"));
        sheet.SetValue(new CellAddress(3, 0), CellValue.Text("Milano"));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(3, 0)));
        sheet.AutoFilter.Set(new ColumnFilter(0) { AllowedValues = new HashSet<string>(["Roma"], StringComparer.CurrentCultureIgnoreCase) });

        foreach (var read in new[] { ThroughXlsx(workbook), ThroughOds(workbook) })
        {
            var filtered = read.Sheets[0];
            Assert.Equal([3], new SheetFilterService().RowsToHide(filtered, filtered.AutoFilter.Range!.Value, filtered.AutoFilter.Filters));
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
