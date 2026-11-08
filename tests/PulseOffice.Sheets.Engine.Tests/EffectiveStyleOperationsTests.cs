using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class EffectiveStyleOperationsTests
{
    private static (Workbook Workbook, Worksheet Sheet, int Bold) BoldColumn()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        var bold = workbook.Styles.Intern(new CellStyle { Font = new CellStyle().Font with { Bold = true } });
        sheet.Layout.SetColumnStyle(0, bold);
        return (workbook, sheet, bold);
    }

    [Fact]
    public void UnisciECentraConservaLoStileDellaColonna()
    {
        var (workbook, sheet, _) = BoldColumn();
        Assert.True(CellRange.TryParseA1("A1:B1", out var range));

        new SheetMerge().Merge(workbook, sheet, range, MergeKind.MergeAndCentre);

        var style = workbook.Styles.Get(sheet.StyleIdAt(new CellAddress(0, 0)));
        Assert.True(style.Font.Bold);
        Assert.Equal(CellHorizontalAlignment.Center, style.Horizontal);
    }

    [Fact]
    public void LaRicercaPerFormatoVedeLoStileDellaColonna()
    {
        var (workbook, sheet, _) = BoldColumn();
        sheet[new CellAddress(0, 0)] = new Cell { Value = CellValue.Text("Titolo") };

        var matches = new SheetSearch().FindAll(
            sheet, "Titolo", new SearchOptions { Style = new StyleFilter { Bold = true } }, styles: workbook.Styles);

        Assert.Single(matches);
    }
}

public class PasteBlankCellsTests
{
    [Fact]
    public void IncollareUnaCellaVuotaSvuotaLaDestinazione()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.SetValue(new CellAddress(0, 4), CellValue.Text("vecchio"));
        Assert.True(CellRange.TryParseA1("A1:B1", out var range));
        var clipboard = new SheetClipboard();

        clipboard.Paste(clipboard.Copy(sheet, range), sheet, new CellAddress(0, 3), range.Start);

        Assert.True(sheet[new CellAddress(0, 4)].Value.IsEmpty);
        Assert.Equal(1, sheet[new CellAddress(0, 3)].Value.AsNumber());
    }
}
