using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Sheets.Addressing;
using Xunit;
using CellValue = PulseOffice.Sheets.Values.CellValue;
using Workbook = PulseOffice.Sheets.Workbook;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class XlsxSharedFormulaTests
{
    [Fact]
    public void LeFormuleCondiviseSiEspandonoInOgniCella()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 5; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row + 1));
            sheet.SetFormula(new CellAddress(row, 1), $"=A{row + 1}*10+$A$1");
        }

        using var stream = new MemoryStream();
        new XlsxWriter().Write(workbook, stream);
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var worksheet = document.WorkbookPart!.WorksheetParts.First().Worksheet!;
            foreach (var cell in worksheet.Descendants<Cell>().Where(cell => cell.CellReference?.Value is "B2" or "B3" or "B4" or "B5"))
            {
                var master = cell.CellReference!.Value == "B2";
                cell.CellFormula = master
                    ? new CellFormula("A2*10+$A$1") { FormulaType = CellFormulaValues.Shared, Reference = "B2:B5", SharedIndex = 0 }
                    : new CellFormula { FormulaType = CellFormulaValues.Shared, SharedIndex = 0 };
            }
            worksheet.Save();
        }

        stream.Position = 0;
        var read = new XlsxReader().Read(stream).Value!.Sheets[0];

        Assert.Equal(new XlsxFormulaSyntax().ToInternal("A5*10+$A$1"), read[new CellAddress(4, 1)].Formula);
        Assert.Equal(new XlsxFormulaSyntax().ToInternal("A3*10+$A$1"), read[new CellAddress(2, 1)].Formula);
        Assert.Equal(new XlsxFormulaSyntax().ToInternal("A1*10+$A$1"), read[new CellAddress(0, 1)].Formula);
    }
}
