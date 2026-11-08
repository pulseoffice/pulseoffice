using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets.Addressing;
using Xunit;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxDataTableTests
{
    private static Workbook Read(Action<Worksheet> alter)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 8; row++)
            for (var column = 0; column < 6; column++)
                sheet.SetValue(new CellAddress(row, column), CellValue.Number(row + column));

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var worksheet = document.WorkbookPart!.WorksheetParts.First().Worksheet!;
            alter(worksheet);
            worksheet.Save();
        }

        stream.Position = 0;
        return new XlsxReader().Read(stream).Value!;
    }

    private static void Mark(Worksheet worksheet, string reference, CellFormula formula) =>
        worksheet.Descendants<Cell>().Single(cell => cell.CellReference?.Value == reference).CellFormula = formula;

    [Fact]
    public void LaTabellaDatiAColonnaDiventaOperazioniMultiple()
    {
        var read = Read(sheet => Mark(sheet, "B5", new CellFormula { FormulaType = CellFormulaValues.DataTable, Reference = "B5:B7", R1 = "A2" }));
        var cells = read.Sheets[0];

        Assert.Equal(new XlsxFormulaSyntax().ToInternal("MULTIPLE.OPERATIONS(B$4,$A$2,$A6)"), cells[new CellAddress(5, 1)].Formula);
        Assert.Equal(CellValue.Number(6), cells[new CellAddress(5, 1)].Value);
        Assert.DoesNotContain(new XlsxSpreadsheetWriter().CheckFidelity(read).Losses, diagnostic => diagnostic.Code == "XLSX082");

        using var again = new MemoryStream();
        new XlsxWriter().Write(read, again);
        again.Position = 0;
        using (var document = SpreadsheetDocument.Open(again, false))
        {
            var formulas = document.WorkbookPart!.WorksheetParts.First().Worksheet!.Descendants<CellFormula>()
                .Where(formula => formula.FormulaType?.Value == CellFormulaValues.DataTable).ToList();
            var table = Assert.Single(formulas);
            Assert.Equal("B5:B7", table.Reference?.Value);
            Assert.Equal("A2", table.R1?.Value);
        }
        again.Position = 0;
        var reread = new XlsxReader().Read(again).Value!.Sheets[0];
        Assert.Equal(cells[new CellAddress(6, 1)].Formula, reread[new CellAddress(6, 1)].Formula);
    }

    [Fact]
    public void LeOperazioniMultipleFuoriDaUnaTabellaDatiSiDichiarano()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].SetFormula(new CellAddress(0, 0), "=MULTIPLE.OPERATIONS(C9;$B$2;$D$1)");

        Assert.Contains(new XlsxSpreadsheetWriter().CheckFidelity(workbook).Losses, diagnostic => diagnostic.Code == "XLSX082");
    }

    [Fact]
    public void LaTabellaDatiARigaEQuellaADueVariabiliSiRicostruiscono()
    {
        var read = Read(sheet =>
        {
            Mark(sheet, "E5", new CellFormula { FormulaType = CellFormulaValues.DataTable, Reference = "E5:F5", DataTableRow = true, R1 = "B2" });
            Mark(sheet, "C7", new CellFormula { FormulaType = CellFormulaValues.DataTable, Reference = "C7:D8", DataTable2D = true, R1 = "A1", R2 = "A2" });
        });
        var cells = read.Sheets[0];

        Assert.Equal(new XlsxFormulaSyntax().ToInternal("MULTIPLE.OPERATIONS($D5,$B$2,F$4)"), cells[new CellAddress(4, 5)].Formula);
        Assert.Equal(new XlsxFormulaSyntax().ToInternal("MULTIPLE.OPERATIONS($B$6,$A$2,$B8,$A$1,D$6)"), cells[new CellAddress(7, 3)].Formula);

        using var again = new MemoryStream();
        new XlsxWriter().Write(read, again);
        again.Position = 0;
        var reread = new XlsxReader().Read(again).Value!.Sheets[0];
        Assert.Equal(cells[new CellAddress(4, 5)].Formula, reread[new CellAddress(4, 5)].Formula);
        Assert.Equal(cells[new CellAddress(7, 3)].Formula, reread[new CellAddress(7, 3)].Formula);
    }
}
