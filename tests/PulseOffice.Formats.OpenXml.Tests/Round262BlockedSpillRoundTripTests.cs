using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Formats.Odf;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round262BlockedSpillRoundTripTests
{
    private readonly CellAddress _anchor = new(0, 0);

    private Workbook BlockedWorkbook()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        sheet[_anchor] = new Cell
        {
            Formula = "=SEQUENCE(3)",
            Value = CellValue.FromError(CellError.Spill),
            ImplicitIntersection = false,
            UsesArrays = true,
        };
        sheet[new CellAddress(1, 0)] = new Cell { Value = CellValue.Number(7) };
        return workbook;
    }

    private Workbook SpilledWorkbook()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Data");
        sheet[_anchor] = new Cell
        {
            Formula = "=SEQUENCE(2)",
            Value = CellValue.Number(1),
            ImplicitIntersection = false,
            SpillRange = new CellRange(_anchor, new CellAddress(1, 0)),
        };
        sheet[new CellAddress(1, 0)] = new Cell { Value = CellValue.Number(2), IsSpilled = true };
        return workbook;
    }

    private Workbook ThroughXlsx(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        stream.Position = 0;
        return new XlsxReader().Read(stream).Value!;
    }

    private Workbook ThroughOds(Workbook workbook)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        return new OdsSpreadsheetReader().Read(stream).Value!;
    }

    [Fact]
    public void Xlsx_writes_a_blocked_anchor_as_a_dynamic_array_with_its_error()
    {
        using var stream = new MemoryStream();
        new XlsxWriter().Write(BlockedWorkbook(), stream);
        using var document = SpreadsheetDocument.Open(stream, false);
        var cell = document.WorkbookPart!.WorksheetParts.First().Worksheet!
            .Descendants<DocumentFormat.OpenXml.Spreadsheet.Cell>().First(found => found.CellReference?.Value == "A1");

        Assert.Equal(XlsxDynamicArrays.CellMetadataIndex, cell.CellMetaIndex!.Value);
        Assert.Equal("#SPILL!", cell.CellValue!.Text);
        Assert.NotNull(document.WorkbookPart.CellMetadataPart);
    }

    [Fact]
    public void A_blocked_anchor_stays_dynamic_across_two_xlsx_round_trips()
    {
        var cell = ThroughXlsx(ThroughXlsx(BlockedWorkbook())).Sheets[0][_anchor];

        Assert.False(cell.ImplicitIntersection);
        Assert.Equal(CellError.Spill, cell.Value.Error);
        Assert.Null(cell.ArrayFormulaRange);
    }

    [Fact]
    public void A_blocked_anchor_stays_dynamic_across_two_ods_round_trips()
    {
        var sheet = ThroughOds(ThroughOds(BlockedWorkbook())).Sheets[0];

        Assert.False(sheet[_anchor].ImplicitIntersection);
        Assert.Equal(CellError.Spill, sheet[_anchor].Value.Error);
        Assert.Equal(7, sheet[new CellAddress(1, 0)].Value.AsNumber());
        Assert.False(sheet[new CellAddress(1, 0)].IsSpilled);
    }

    [Fact]
    public void Ods_keeps_the_cached_values_of_spilled_cells()
    {
        var sheet = ThroughOds(SpilledWorkbook()).Sheets[0];

        Assert.Equal(2, sheet[new CellAddress(1, 0)].Value.AsNumber());
        Assert.True(sheet[new CellAddress(1, 0)].IsSpilled);
        Assert.False(sheet[new CellAddress(1, 0)].HasFormula);
    }

    [Fact]
    public void Xlsx_keeps_the_cached_values_of_spilled_cells_without_formulas()
    {
        var sheet = ThroughXlsx(SpilledWorkbook()).Sheets[0];

        Assert.Equal(2, sheet[new CellAddress(1, 0)].Value.AsNumber());
        Assert.True(sheet[new CellAddress(1, 0)].IsSpilled);
        Assert.False(sheet[new CellAddress(1, 0)].HasFormula);
    }
}
