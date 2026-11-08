using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Formatting;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class ScopedNamesAndMultiRangeRuleTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Fact]
    public void NomiLocaliUgualiSuFogliDiversiConvivono()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.AddSheet("Nord");
        workbook.DefineName(new DefinedName("Tasso", "0.1"));
        workbook.DefineName(new DefinedName("Tasso", "0.2", "Sheet1"));
        workbook.DefineName(new DefinedName("Tasso", "0.3", "Nord"));

        Assert.Equal(3, workbook.DefinedNames.Count);

        engine.SetFormula(new SheetCell(0, A("A1")), "=Tasso*10");
        engine.SetFormula(new SheetCell(1, A("A1")), "=Tasso*10");

        Assert.Equal(2, engine.GetValue(new SheetCell(0, A("A1"))).AsNumber(), 10);
        Assert.Equal(3, engine.GetValue(new SheetCell(1, A("A1"))).AsNumber(), 10);

        workbook.RemoveName("Tasso", "Nord");
        engine.RecalculateAll();

        Assert.Equal(1, engine.GetValue(new SheetCell(1, A("A1"))).AsNumber(), 10);
        Assert.Equal("0.2", workbook.FindName("Tasso", "sheet1")!.Expression);
    }

    [Fact]
    public void UnNomeLocaleSeguaLeDipendenzeDelProprioFoglio()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.AddSheet("Nord");
        workbook.DefineName(new DefinedName("Base", "Sheet1.B1", "Sheet1"));
        workbook.DefineName(new DefinedName("Base", "Nord.B1", "Nord"));

        engine.SetValue(new SheetCell(1, A("B1")), CellValue.Number(5));
        engine.SetFormula(new SheetCell(1, A("A1")), "=Base+1");
        engine.SetValue(new SheetCell(1, A("B1")), CellValue.Number(9));

        Assert.Equal(10, engine.GetValue(new SheetCell(1, A("A1"))).AsNumber(), 10);
    }

    [Fact]
    public void RinominareUnFoglioSpostaIlSuoNomeLocaleSenzaToccareGliAltri()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var nord = workbook.AddSheet("Nord");
        workbook.DefineName(new DefinedName("Base", "1", "Sheet1"));
        workbook.DefineName(new DefinedName("Base", "2", "Nord"));

        var renaming = new SheetRenaming();
        var plan = renaming.Plan(workbook, nord, "Sud");
        renaming.Apply(engine, plan);

        Assert.Equal("2", workbook.FindName("Base", "Sud")!.Expression);
        Assert.Equal("1", workbook.FindName("Base", "Sheet1")!.Expression);
        Assert.Null(workbook.FindName("Base", "Nord"));

        renaming.Revert(engine, plan);

        Assert.Equal("2", workbook.FindName("Base", "Nord")!.Expression);
        Assert.Equal(2, workbook.DefinedNames.Count);
    }

    [Fact]
    public void UnaRegolaSuPiuAreeUsaLaFormulaRelativaAllAncora()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        foreach (var (cell, value) in new[] { ("A1", 5.0), ("A2", -1.0), ("D1", -3.0), ("D2", 7.0) })
            engine.SetValue(new SheetCell(0, A(cell)), CellValue.Number(value));

        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A2")),
            AdditionalRanges = [new CellRange(A("D1"), A("D2"))],
            Formula = "A1>0",
            Style = new CellStyle { BackgroundHex = "#00FF00" },
        });

        var evaluator = new ConditionalFormattingEvaluator(fixture.Evaluator);
        bool Lit(string a1) => !evaluator.Evaluate(sheet, A(a1), engine.ContextAt(new SheetCell(0, A(a1)))).IsEmpty;

        Assert.True(Lit("A1"));
        Assert.False(Lit("A2"));
        Assert.False(Lit("D1"));
        Assert.True(Lit("D2"));
        Assert.False(Lit("B1"));
    }

    [Fact]
    public void LeBarreSuPiuAreeSiMisuranoSuTutteLeAree()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.SetValue(A("A1"), CellValue.Number(0));
        sheet.SetValue(A("C1"), CellValue.Number(100));
        sheet.SetValue(A("C2"), CellValue.Number(50));
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A1")),
            AdditionalRanges = [new CellRange(A("C1"), A("C2"))],
            Visual = ConditionalVisual.DataBar,
        });

        var evaluator = new ConditionalFormattingEvaluator(fixture.Evaluator);

        Assert.Equal(0.5, evaluator.Evaluate(sheet, A("C2")).BarFraction!.Value, 6);
    }

    [Fact]
    public void EliminandoLaPrimaAreaLaFormulaSiRiancoraAllaSeconda()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.ConditionalRules.Add(new ConditionalRule
        {
            Range = new CellRange(A("A1"), A("A2")),
            AdditionalRanges = [new CellRange(A("D1"), A("D2"))],
            Formula = "A1>0",
        });

        new SheetStructure().DeleteColumns(engine, 0, 0, 1);

        var rule = Assert.Single(sheet.ConditionalRules);
        Assert.Equal(new CellRange(A("C1"), A("C2")), rule.Range);
        Assert.Empty(rule.AdditionalRanges);
        Assert.Equal("C1>0", rule.Formula);
    }
}

public class ScopedNamesSheetRemovalTests
{
    [Fact]
    public void EliminandoUnFoglioISuoiNomiLocaliSparisconoETornanoAnnullando()
    {
        var workbook = Workbook.CreateDefault();
        var nord = workbook.AddSheet("Nord");
        workbook.DefineName(new DefinedName("Tasso", "0.3", "Nord"));
        workbook.DefineName(new DefinedName("Tasso", "0.1"));

        Assert.True(workbook.RemoveSheet(nord));
        Assert.Null(workbook.FindName("Tasso", "Nord"));
        Assert.Single(workbook.DefinedNames);

        workbook.InsertSheet(nord, 1);
        Assert.Equal("0.3", workbook.FindName("Tasso", "Nord")!.Expression);
        Assert.Equal(2, workbook.DefinedNames.Count);
    }
}
