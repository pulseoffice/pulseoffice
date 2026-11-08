using System.IO.Compression;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using Sdk = DocumentFormat.OpenXml.Spreadsheet;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round126SortStateTests
{
    private static readonly CellRange Block = new(new CellAddress(1, 1), new CellAddress(5, 3));

    private static Workbook Book(SortSettings settings, bool header = true)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 1; row <= 5; row++)
            for (var column = 1; column <= 3; column++)
                sheet.SetValue(new CellAddress(row, column), row == 1 ? CellValue.Text("H" + column) : CellValue.Number(row * column));
        sheet.RangeSort = new SheetRangeSort(Block, header, settings);
        return workbook;
    }

    [Fact]
    public void A_plain_range_sort_crosses_both_formats()
    {
        var settings = new SortSettings { Fields = [new SortField(2, false), new SortField(0)], CaseSensitive = true };

        var odsSort = ThroughOds(Book(settings)).Sheets[0].RangeSort;
        Assert.NotNull(odsSort);
        Assert.Equal((Block, true, settings.Orientation, settings.CaseSensitive), (odsSort.Range, odsSort.HasHeader, odsSort.Settings.Orientation, odsSort.Settings.CaseSensitive));
        Assert.Equal(settings.Fields, odsSort.Settings.Fields);

        var xlsxSort = ThroughXlsx(Book(settings)).Sheets[0].RangeSort;
        Assert.NotNull(xlsxSort);
        Assert.Equal(new CellRange(new CellAddress(2, 1), Block.End), xlsxSort.Data);
        Assert.Equal(settings.Fields, xlsxSort.Settings.Fields);
        Assert.True(xlsxSort.Settings.CaseSensitive);
    }

    [Fact]
    public void A_left_to_right_sort_crosses_both_formats()
    {
        var settings = new SortSettings { Fields = [new SortField(3, false)], Orientation = SortOrientation.LeftToRight };

        var odsSort = ThroughOds(Book(settings)).Sheets[0].RangeSort;
        Assert.NotNull(odsSort);
        Assert.Equal((Block, true, settings.Orientation, settings.CaseSensitive), (odsSort.Range, odsSort.HasHeader, odsSort.Settings.Orientation, odsSort.Settings.CaseSensitive));
        Assert.Equal(settings.Fields, odsSort.Settings.Fields);

        var xlsxSort = ThroughXlsx(Book(settings)).Sheets[0].RangeSort;
        Assert.NotNull(xlsxSort);
        Assert.True(xlsxSort.Settings.LeftToRight);
        Assert.Equal(new CellRange(new CellAddress(1, 2), Block.End), xlsxSort.Data);
        Assert.Equal(settings.Fields, xlsxSort.Settings.Fields);
    }

    [Fact]
    public void The_xlsx_worksheet_sort_state_is_written_for_a_plain_range()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Book(new SortSettings { Fields = [new SortField(1)], Orientation = SortOrientation.LeftToRight }), stream);
        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, false);
        var state = document.WorkbookPart!.WorksheetParts.First().Worksheet!.GetFirstChild<Sdk.SortState>();

        Assert.NotNull(state);
        Assert.Equal("C2:D6", state.Reference!.Value);
        Assert.True(state.ColumnSort!.Value);
        Assert.Equal("C3:D3", state.Elements<Sdk.SortCondition>().Single().Reference!.Value);
    }

    [Fact]
    public void A_worksheet_sort_state_written_by_excel_on_a_plain_range_is_read()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(Book(new SortSettings { Fields = [new SortField(0)] }), stream);
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var worksheet = document.WorkbookPart!.WorksheetParts.First().Worksheet!;
            worksheet.RemoveAllChildren<Sdk.SortState>();
            var state = new Sdk.SortState { Reference = "B3:D6" };
            state.Append(new Sdk.SortCondition { Reference = "D3:D6", Descending = true });
            worksheet.InsertAfter(state, worksheet.GetFirstChild<Sdk.SheetData>());
            worksheet.Save();
        }
        stream.Position = 0;

        var sort = new XlsxReader().Read(stream).Value!.Sheets[0].RangeSort;

        Assert.NotNull(sort);
        Assert.Equal(new CellRange(new CellAddress(2, 1), Block.End), sort.Range);
        Assert.Equal([new SortField(2, false)], sort.Settings.Fields);
    }

    [Fact]
    public void The_ods_anonymous_range_carries_the_orientation()
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(Book(new SortSettings { Fields = [new SortField(1)], Orientation = SortOrientation.LeftToRight }), stream);
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        using var content = zip.GetEntry("content.xml")!.Open();
        XNamespace table = "urn:oasis:names:tc:opendocument:xmlns:table:1.0";
        var range = XDocument.Load(content).Descendants(table + "database-range").Single();

        Assert.Equal("column", (string?)range.Attribute(table + "orientation"));
        Assert.Equal("1", (string?)range.Element(table + "sort")!.Element(table + "sort-by")!.Attribute(table + "field-number"));
    }

    [Fact]
    public void A_plain_range_sort_that_cannot_share_the_sheet_database_range_is_declared_for_ods()
    {
        var workbook = Book(new SortSettings { Fields = [new SortField(0)] });
        Assert.DoesNotContain(new OdsSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code.StartsWith("ODF27", StringComparison.Ordinal));

        workbook.Sheets[0].AutoFilter.Activate(new CellRange(new CellAddress(10, 0), new CellAddress(14, 2)));

        Assert.Contains(new OdsSpreadsheetWriter().CheckFidelity(workbook).Losses, loss => loss.Code == "ODF274");
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
