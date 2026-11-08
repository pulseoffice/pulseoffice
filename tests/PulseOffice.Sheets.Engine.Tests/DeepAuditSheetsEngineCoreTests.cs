using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DeepAuditSheetsEngineCoreTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Theory]
    [InlineData("=SUBTOTAL(9;A2:A6)", 13)]
    [InlineData("=SUBTOTAL(109;A2:A6)", 9)]
    [InlineData("=AGGREGATE(9;5;A2:A6)", 9)]
    [InlineData("=AGGREGATE(9;4;A2:A6)", 15)]
    [InlineData("=SUM(A2:A6)", 15)]
    public void SubtotalEAggregateEscludonoLeRigheFiltrateONascosteComeNelRiferimento(string formula, double atteso)
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        for (var row = 1; row <= 5; row++)
            engine.SetValue(new SheetCell(0, new CellAddress(row, 0)), CellValue.Number(row));
        sheet.AutoFilter.Activate(new CellRange(new CellAddress(0, 0), new CellAddress(3, 0)));
        sheet.AutoFilter.Set(new ColumnFilter(0) { AllowedValues = new HashSet<string> { "1", "3" } });
        sheet.Layout.HideRow(2);
        sheet.Layout.HideRow(4);
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 7)), formula);
        Assert.Equal(atteso, engine.GetValue(new SheetCell(0, new CellAddress(0, 7))).AsNumber());
    }

    public static TheoryData<string> FormuleAnnidateOltreMisura() => new()
    {
        "=" + new string('(', 100_000) + "1" + new string(')', 100_000),
        "=" + new string('-', 100_000) + "1",
        "=1" + string.Concat(Enumerable.Repeat("+1", 100_000)),
        "=" + string.Concat(Enumerable.Repeat("ABS(", 100_000)) + "1" + new string(')', 100_000),
        "={" + string.Concat(Enumerable.Repeat("(", 100_000)) + "1" + new string(')', 100_000) + "}",
        "=1" + string.Concat(Enumerable.Repeat("^1", 100_000)),
        "=A1" + string.Concat(Enumerable.Repeat("~A1", 100_000)),
        "=1" + string.Concat(Enumerable.Repeat("%", 100_000)),
    };

    [Theory]
    [MemberData(nameof(FormuleAnnidateOltreMisura))]
    public void UnaFormulaAnnidataOltreMisuraDaErroreSenzaTraboccareLoStack(string formula)
    {
        var (_, engine) = fixture.CreateWorkbook();
        CellValue? risultato = null;
        var thread = new Thread(() =>
        {
            engine.SetFormula(new SheetCell(0, new CellAddress(0, 7)), formula);
            risultato = engine.GetValue(new SheetCell(0, new CellAddress(0, 7)));
        }, 1024 * 1024);
        thread.Start();
        thread.Join();
        Assert.True(risultato is { IsError: true }, risultato?.ToString());
    }

    [Fact]
    public void UnaCatenaLungaMaAmmessaSiCalcolaSuUnoStackDiUnMegabyte()
    {
        var (_, engine) = fixture.CreateWorkbook();
        CellValue? risultato = null;
        var thread = new Thread(() =>
        {
            engine.SetFormula(new SheetCell(0, new CellAddress(0, 7)), "=1" + string.Concat(Enumerable.Repeat("+1", 1400)) + "&" + new string('(', 60) + "1" + new string(')', 60));
            risultato = engine.GetValue(new SheetCell(0, new CellAddress(0, 7)));
        }, 1024 * 1024);
        thread.Start();
        thread.Join();
        Assert.Equal("14011", risultato?.AsText());
    }

    [Theory]
    [InlineData("=SUM(Città.A:A)")]
    [InlineData("=SUM(Übersicht.3:5)")]
    [InlineData("=SUM(Città.$B:$C)")]
    public void LeColonneInterediUnFoglioConNomeUnicodeSiRileggono(string formula)
    {
        var node = new Parsing.FormulaParser().Parse(formula);
        var range = Assert.IsType<Parsing.RangeNode>(Assert.IsType<Parsing.FunctionNode>(node).Arguments[0]);
        Assert.NotNull(range.SheetName);
    }

    [Fact]
    public void UnaCatenaDiCentomilaCelleSiRicalcolaSenzaTraboccareLoStack()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        for (var row = 1; row < 100_000; row++)
            sheet.SetFormula(new CellAddress(row, 0), $"=A{row}+1");
        Exception? errore = null;
        var thread = new Thread(() =>
        {
            try { engine.RecalculateAll(); }
            catch (Exception e) { errore = e; }
        }, 1024 * 1024);
        thread.Start();
        thread.Join();
        Assert.Null(errore);
        Assert.Equal(100_000, engine.GetValue(new SheetCell(0, new CellAddress(99_999, 0))).AsNumber());
    }

    [Fact]
    public void MoltiIntervalliSiOrdinanoSenzaCostoQuadratico()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 20_000; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(1));
            sheet.SetFormula(new CellAddress(row, 2), $"=SUM(A{row + 1}:B{row + 1})");
        }
        var watch = System.Diagnostics.Stopwatch.StartNew();
        engine.RecalculateAll();
        engine.SetValue(new SheetCell(0, new CellAddress(5, 0)), CellValue.Number(3));
        Assert.True(watch.ElapsedMilliseconds < 3000, $"{watch.ElapsedMilliseconds} ms");
        Assert.Equal(3, engine.GetValue(new SheetCell(0, new CellAddress(5, 2))).AsNumber());
    }

    [Fact]
    public void UnNomeCheRimandaASeStessoNonFaCrollareIlRicalcolo()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.DefineName(new DefinedName("Giro", "Anello+1"));
        workbook.DefineName(new DefinedName("Anello", "Giro*2"));
        CellValue? risultato = null;
        var thread = new Thread(() =>
        {
            engine.SetFormula(new SheetCell(0, new CellAddress(0, 0)), "=Giro");
            risultato = engine.GetValue(new SheetCell(0, new CellAddress(0, 0)));
        }, 1024 * 1024);
        thread.Start();
        thread.Join();
        Assert.True(risultato is { IsError: true }, risultato?.ToString());
    }

    [Theory]
    [InlineData("=SUM(B2#)", 1, 0, "=SUM(B3#)")]
    [InlineData("=$B$2#", 1, 1, "=$B$2#")]
    [InlineData("=B$2#*2", 3, 1, "=C$2#*2")]
    public void IRiferimentiAUnoSversamentoSiSpostanoCopiando(string formula, int righe, int colonne, string atteso) =>
        Assert.Equal(atteso, new Operations.FormulaTranslator().Translate(formula, righe, colonne));

    [Fact]
    public void IRiferimentiAUnoSversamentoSeguonoLeRigheInserite() =>
        Assert.Equal("=SUM(B4#)", new Operations.StructureRewriter(
            new Operations.StructureChange(Operations.StructureAxis.Rows, 0, 2, true, "Foglio1"), "Foglio1").Rewrite("=SUM(B2#)"));

    [Fact]
    public void UnoSversamentoConLaCellaEliminataDiventaRef() =>
        Assert.Equal("=SUM(#REF!)", new Operations.StructureRewriter(
            new Operations.StructureChange(Operations.StructureAxis.Rows, 1, 1, false, "Foglio1"), "Foglio1").Rewrite("=SUM(B2#)"));

    [Fact]
    public void UnoSversamentoRipristinatoDaUnoScattoNonVaInSpill()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 0)), "=SEQUENCE(3)");
        var snapshots = new Operations.SheetSnapshots();
        var before = snapshots.Capture(workbook.Sheets[0]);
        new Operations.SheetStructure().InsertRows(engine, 0, 0, 1);
        snapshots.Restore(engine, 0, before);
        Assert.Equal(1, engine.GetValue(new SheetCell(0, new CellAddress(0, 0))).AsNumber());
        Assert.Equal(2, engine.GetValue(new SheetCell(0, new CellAddress(1, 0))).AsNumber());
        Assert.Equal(3, engine.GetValue(new SheetCell(0, new CellAddress(2, 0))).AsNumber());
        Assert.True(engine.GetValue(new SheetCell(0, new CellAddress(3, 0))).IsEmpty);
    }

    [Fact]
    public void UnoSversamentoSopravviveAUnaRigaInseritaSopra()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(new SheetCell(0, new CellAddress(0, 0)), "=SEQUENCE(3)");
        new Operations.SheetStructure().InsertRows(engine, 0, 0, 1);
        Assert.Equal(1, engine.GetValue(new SheetCell(0, new CellAddress(1, 0))).AsNumber());
        Assert.Equal(3, engine.GetValue(new SheetCell(0, new CellAddress(3, 0))).AsNumber());
    }

    [Fact]
    public void UnaFormulaMatriceSopravviveAUnaRigaInseritaSopra()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        engine.SetValue(new SheetCell(0, new CellAddress(0, 0)), CellValue.Number(2));
        engine.SetArrayFormula(new SheetCell(0, new CellAddress(0, 2)),
            new CellRange(new CellAddress(0, 2), new CellAddress(1, 2)), "=A1*{1|2}");
        new Operations.SheetStructure().InsertRows(engine, 0, 0, 1);
        Assert.Equal(new CellRange(new CellAddress(1, 2), new CellAddress(2, 2)), workbook.Sheets[0][new CellAddress(2, 2)].ArrayFormulaRange);
        Assert.False(workbook.Sheets[0][new CellAddress(0, 2)].IsPartOfArrayFormula);
        Assert.Equal("=A2*{1|2}", workbook.Sheets[0][new CellAddress(1, 2)].Formula);
        Assert.Equal(4, engine.GetValue(new SheetCell(0, new CellAddress(2, 2))).AsNumber());
    }

    [Fact]
    public void InserireUnaRigaInCimaAMoltiDatiNonRicalcolaPerOgniCella()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        engine.AutomaticCalculation = false;
        for (var row = 0; row < 5_000; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
            sheet.SetFormula(new CellAddress(row, 1), $"=SUM(A$1:A{row + 1})");
        }
        engine.AutomaticCalculation = true;
        engine.RecalculateAll();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        new Operations.SheetStructure().InsertRows(engine, 0, 0, 1);
        Assert.True(watch.ElapsedMilliseconds < 5000, $"{watch.ElapsedMilliseconds} ms");
        Assert.Equal("=SUM(A$2:A5001)", sheet[new CellAddress(5_000, 1)].Formula);
    }
}
