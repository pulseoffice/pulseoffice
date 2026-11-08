using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class AdvancedFilterCopyTests
{
    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    private static (Workbook, Worksheet, Worksheet) Setup()
    {
        var workbook = new Workbook();
        var data = workbook.AddSheet("Dati");
        var criteria = workbook.AddSheet("Criteri 2");
        data.SetValue(new CellAddress(0, 0), CellValue.Text("Zona"));
        data.SetValue(new CellAddress(0, 1), CellValue.Text("Valore"));
        data.SetValue(new CellAddress(1, 0), CellValue.Text("Nord"));
        data.SetValue(new CellAddress(1, 1), CellValue.Number(10));
        data.SetValue(new CellAddress(2, 0), CellValue.Text("Sud"));
        data.SetValue(new CellAddress(2, 1), CellValue.Number(20));
        data.SetValue(new CellAddress(3, 0), CellValue.Text("Nord"));
        data.SetValue(new CellAddress(3, 1), CellValue.Number(30));
        criteria.SetValue(new CellAddress(0, 0), CellValue.Text("Zona"));
        criteria.SetValue(new CellAddress(1, 0), CellValue.Text("Nord"));
        return (workbook, data, criteria);
    }

    [Fact]
    public void ICriteriPossonoStareSuUnAltroFoglio()
    {
        var (_, data, criteria) = Setup();

        var hidden = new SheetFilterService().RowsToHide(
            data, new AdvancedFilterRequest(R("A1:B4"), R("A1:A2")) { CriteriaSheet = criteria });

        Assert.Equal([2], hidden.Order());
    }

    [Fact]
    public void CopiaLeRigheCheCorrispondonoConLIntestazione()
    {
        var (_, data, criteria) = Setup();

        var cells = new SheetFilterService().CopyMatches(
            data, new AdvancedFilterRequest(R("A1:B4"), R("A1:A2")) { CriteriaSheet = criteria }, new CellAddress(0, 4));

        var values = cells.ToDictionary(pair => pair.Address, pair => pair.Cell.Value.ToString());
        Assert.Equal(6, cells.Count);
        Assert.Equal("Zona", values[new CellAddress(0, 4)]);
        Assert.Equal("Nord", values[new CellAddress(2, 4)]);
        Assert.Equal("30", values[new CellAddress(2, 5)]);
    }

    [Fact]
    public void LeFormuleCopiateSiTraslano()
    {
        var (_, data, criteria) = Setup();
        data.SetFormula(new CellAddress(3, 1), "=B2*3");

        var cells = new SheetFilterService().CopyMatches(
            data, new AdvancedFilterRequest(R("A1:B4"), R("A1:A2")) { CriteriaSheet = criteria }, new CellAddress(0, 4));

        Assert.Equal("=F1*3", cells.Single(pair => pair.Address == new CellAddress(2, 5)).Cell.Formula);
    }

    [Theory]
    [InlineData("'Criteri 2'.A1:A2", 1)]
    [InlineData("Criteri 2!$A$1:$A$2", 1)]
    [InlineData("A1:B4", 0)]
    [InlineData("$Dati.A1:Dati.B4", 0)]
    public void LeggeGliIntervalliConIlFoglio(string text, int sheet)
    {
        var (workbook, _, _) = Setup();

        var reference = new SheetRangeReferences().Parse(workbook, text, 0);

        Assert.NotNull(reference);
        Assert.Equal(sheet, reference.SheetIndex);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Inesistente.A1")]
    [InlineData("zzz")]
    public void RifiutaGliIntervalliNonValidi(string text)
    {
        var (workbook, _, _) = Setup();

        Assert.Null(new SheetRangeReferences().Parse(workbook, text, 0));
    }
}

public class SortCommentsTests
{
    [Fact]
    public void ICommentiSiSpostanoConLeRighe()
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(2));
        sheet.SetValue(new CellAddress(1, 0), CellValue.Number(1));
        sheet.Comments[new CellAddress(0, 0)] = new CellComment { Text = "due" };
        Assert.True(CellRange.TryParseA1("A1:A2", out var range));
        var history = new PulseOffice.Document.Commands.UndoStack();

        new SheetSorter().Sort(sheet, range, [new SortKey(0)], history: history);

        Assert.Equal("due", sheet.Comments[new CellAddress(1, 0)].Text);
        Assert.False(sheet.Comments.ContainsKey(new CellAddress(0, 0)));

        history.Undo();
        Assert.Equal("due", sheet.Comments[new CellAddress(0, 0)].Text);
        Assert.False(sheet.Comments.ContainsKey(new CellAddress(1, 0)));
    }
}
