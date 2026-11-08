using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;
using X = DocumentFormat.OpenXml.Spreadsheet;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxTableTotalsTests
{
    private static readonly CellRange SalesRange = new(CellAddress.First, new CellAddress(4, 1));

    private Workbook WithTotals()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Name"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Amount"));
        for (var row = 1; row <= 3; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text("n" + row));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Number(row * 10));
        }
        sheet.SetValue(new CellAddress(4, 0), CellValue.Text("Total"));
        sheet.SetFormula(new CellAddress(4, 1), "=SUBTOTAL(109;B2:B4)");
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = SalesRange, HasTotalsRow = true, ShowsFilterButtons = true });
        return workbook;
    }

    private MemoryStream Xlsx(Workbook workbook)
    {
        var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return stream;
    }

    private Workbook ThroughXlsx(Workbook workbook)
    {
        using var stream = Xlsx(workbook);
        return new XlsxReader().Read(stream).Value!;
    }

    private Workbook ThroughOds(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        return new OdsReader().Read(stream).Value!;
    }

    [Fact]
    public void The_totals_row_of_a_table_is_written_as_a_totals_row()
    {
        using var stream = Xlsx(WithTotals());
        using var document = SpreadsheetDocument.Open(stream, false);

        var table = document.WorkbookPart!.WorksheetParts.Single().TableDefinitionParts.Single().Table!;
        var columns = table.TableColumns!.Elements<X.TableColumn>().ToList();

        Assert.Equal(1u, table.TotalsRowCount?.Value);
        Assert.Equal("A1:B5", table.Reference?.Value);
        Assert.Equal("A1:B4", table.AutoFilter?.Reference?.Value);
        Assert.Equal("Total", columns[0].TotalsRowLabel?.Value);
        Assert.Equal(X.TotalsRowFunctionValues.Custom, columns[1].TotalsRowFunction?.Value);
        Assert.Equal("SUBTOTAL(109,B2:B4)", columns[1].TotalsRowFormula?.Text);
    }

    [Fact]
    public void The_totals_row_crosses_xlsx_and_ods()
    {
        foreach (var read in new[] { ThroughXlsx(WithTotals()), ThroughOds(WithTotals()), ThroughOds(ThroughXlsx(WithTotals())), ThroughXlsx(ThroughOds(WithTotals())) })
        {
            var range = read.DatabaseRanges.Find("Sales")!;
            Assert.True(range.HasTotalsRow);
            Assert.Equal(SalesRange, range.Range);
            Assert.True(read.Sheets[0][new CellAddress(4, 1)].HasFormula);
        }
    }

    [Fact]
    public void A_table_with_totals_written_by_another_application_keeps_them()
    {
        var workbook = WithTotals();
        using var stream = Xlsx(workbook);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var table = document.WorkbookPart!.WorksheetParts.Single().TableDefinitionParts.Single().Table!;
            table.RemoveAllChildren<X.AutoFilter>();
            table.Save();
        }
        stream.Position = 0;

        var range = new XlsxReader().Read(stream).Value!.DatabaseRanges.Find("Sales")!;

        Assert.True(range.HasTotalsRow);
        Assert.Equal(new CellRange(CellAddress.First, new CellAddress(3, 1)), range.RangeWithoutTotals);
    }

    [Fact]
    public void A_database_range_named_like_a_query_table_does_not_collide()
    {
        var workbook = WithTotals();
        workbook.DatabaseRanges.Remove("Sales");
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "ExternalData_1", SheetIndex = 0, Range = SalesRange, HasTotalsRow = true });
        workbook.Sheets[0].QueryBindings.AddOrUpdate(new QueryBinding
        {
            Id = "q1",
            DisplayName = "Customers",
            ConnectionId = "crm",
            Query = new PulseOffice.Data.QueryDefinition { Sql = "SELECT * FROM customers" },
            Anchor = new CellAddress(10, 5),
            Range = new CellRange(new CellAddress(10, 5), new CellAddress(12, 6)),
        });

        using var stream = Xlsx(workbook);
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var names = document.WorkbookPart!.Workbook!.DefinedNames!.Elements<X.DefinedName>().Select(name => name.Name!.Value!).ToList();
            var tables = document.WorkbookPart.WorksheetParts.Single().TableDefinitionParts.Select(part => part.Table!.Name!.Value!).ToList();
            Assert.Empty(names.Intersect(tables, StringComparer.OrdinalIgnoreCase));
            Assert.Equal(["ExternalData_1"], tables);
        }
        stream.Position = 0;

        var read = new XlsxReader().Read(stream).Value!;
        Assert.True(read.DatabaseRanges.Find("ExternalData_1")!.HasTotalsRow);
        Assert.Equal(new CellRange(new CellAddress(10, 5), new CellAddress(12, 6)), Assert.Single(read.Sheets[0].QueryBindings.All).Range);
    }
}
