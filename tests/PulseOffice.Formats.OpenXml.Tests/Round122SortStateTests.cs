using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round122SortStateTests
{
    private static readonly SortSettings Settings = new()
    {
        Fields = [new SortField(1, false), new SortField(0) { CustomOrder = ["b", "a", "c"] }],
        CaseSensitive = true,
    };

    private static Workbook Book()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 4; row++)
            for (var column = 0; column < 6; column++)
                sheet.SetValue(new CellAddress(row, column), row == 0 ? CellValue.Text("H" + column) : CellValue.Number(row * column));
        sheet.AutoFilter.Activate(new CellRange(CellAddress.First, new CellAddress(3, 2)));
        sheet.AutoFilter.Remember(Settings);
        workbook.DatabaseRanges.Define(new DatabaseRange
        {
            Name = "Data", SheetIndex = 0, Range = new CellRange(new CellAddress(0, 4), new CellAddress(3, 5)),
            ShowsFilterButtons = true, Sort = new SortSettings { Fields = [new SortField(1, false)] },
        });
        return workbook;
    }

    [Fact]
    public void Autofilter_and_database_range_sort_settings_cross_both_formats()
    {
        foreach (var read in new[] { ThroughXlsx(Book()), ThroughOds(Book()) })
        {
            var sort = read.Sheets[0].AutoFilter.Sort;
            Assert.NotNull(sort);
            Assert.True(sort.CaseSensitive);
            Assert.Equal(2, sort.Fields.Count);
            Assert.Equal(new SortField(1, false), sort.Fields[0]);
            Assert.Equal(0, sort.Fields[1].ColumnOffset);
            Assert.Equal(["b", "a", "c"], sort.Fields[1].CustomOrder!);

            var table = read.DatabaseRanges.Find("Data")!.Sort;
            Assert.NotNull(table);
            Assert.Equal([new SortField(1, false)], table.Fields);
        }
    }

    [Fact]
    public void The_xlsx_sort_state_refers_to_the_data_rows_below_the_header()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Book(), stream);
        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, false);
        var state = document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<DocumentFormat.OpenXml.Spreadsheet.SortState>().First();

        Assert.Equal("A2:C4", state.Reference!.Value);
        Assert.Equal("B2:B4", state.Elements<DocumentFormat.OpenXml.Spreadsheet.SortCondition>().First().Reference!.Value);
    }

    [Fact]
    public void A_custom_order_item_with_a_comma_is_declared_for_xlsx()
    {
        var workbook = Book();
        Assert.DoesNotContain("XLSX236", new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses.Select(loss => loss.Code));

        workbook.Sheets[0].AutoFilter.Remember(new SortSettings { Fields = [new SortField(0) { CustomOrder = ["Rome, Italy", "Paris"] }] });

        Assert.Contains("XLSX236", new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses.Select(loss => loss.Code));
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
