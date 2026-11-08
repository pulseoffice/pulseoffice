using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class SheetStructureTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly ISheetStructure Structure = new SheetStructure();

    private static SheetCell Cell(int row, int column = 0) => new(0, new CellAddress(row, column));

    [Fact]
    public void InserendoUnaRigaIlContenutoScendeELeFormuleSeguono()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0), CellValue.Number(1));
        engine.SetValue(Cell(1), CellValue.Number(2));
        engine.SetFormula(Cell(2), "=A1+A2");

        Structure.InsertRows(engine, 0, 1, 1);

        var sheet = workbook.Sheets[0];

        Assert.Equal(1, sheet.GetValue(new CellAddress(0, 0)).AsNumber());
        Assert.True(sheet.IsBlank(new CellAddress(1, 0)));
        Assert.Equal(2, sheet.GetValue(new CellAddress(2, 0)).AsNumber());
        Assert.Equal("=A1+A3", sheet[new CellAddress(3, 0)].Formula);
        Assert.Equal(3, engine.GetValue(Cell(3)).AsNumber());
    }

    [Fact]
    public void EliminandoUnaRigaIlContenutoRisaleELIntervalloSiRestringe()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        for (var row = 0; row < 4; row++) engine.SetValue(Cell(row), CellValue.Number(row + 1));
        engine.SetFormula(Cell(5), "=SUM(A1:A4)");

        Structure.DeleteRows(engine, 0, 1, 1);

        var sheet = workbook.Sheets[0];

        Assert.Equal(3, sheet.GetValue(new CellAddress(1, 0)).AsNumber());
        Assert.Equal("=SUM(A1:A3)", sheet[new CellAddress(4, 0)].Formula);
        Assert.Equal(8, engine.GetValue(Cell(4)).AsNumber());
    }

    [Fact]
    public void UnaFormulaCheIndicaUnaCellaEliminataDiventaUnErrore()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(3), CellValue.Number(5));
        engine.SetFormula(Cell(0), "=A4*2");

        Structure.DeleteRows(engine, 0, 3, 1);

        Assert.Contains("#REF!", workbook.Sheets[0][new CellAddress(0, 0)].Formula!, StringComparison.Ordinal);
    }

    [Fact]
    public void LeColonneSiInserisconoComeLeRighe()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(0, 0), CellValue.Number(1));
        engine.SetValue(Cell(0, 1), CellValue.Number(2));
        engine.SetFormula(Cell(0, 2), "=A1+B1");

        Structure.InsertColumns(engine, 0, 1, 1);

        var sheet = workbook.Sheets[0];

        Assert.True(sheet.IsBlank(new CellAddress(0, 1)));
        Assert.Equal(2, sheet.GetValue(new CellAddress(0, 2)).AsNumber());
        Assert.Equal("=A1+C1", sheet[new CellAddress(0, 3)].Formula);
    }

    [Fact]
    public void AncheIRiferimentiAssolutiSiSpostano()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell(4), CellValue.Number(7));
        engine.SetFormula(Cell(0), "=$A$5");

        Structure.InsertRows(engine, 0, 1, 2);

        Assert.Equal("=$A$7", workbook.Sheets[0][new CellAddress(0, 0)].Formula);
    }

    [Fact]
    public void LeAltezzeDelleRigheSeguonoLoSpostamento()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];

        sheet.Layout.SetRowHeight(3, Twip.FromPoints(40));

        Structure.InsertRows(engine, 0, 1, 1);

        Assert.Equal(40, sheet.Layout.RowHeight(4).ToPoints(), 3);
        Assert.Equal(sheet.Layout.DefaultRowHeight, sheet.Layout.RowHeight(3));
    }

    [Fact]
    public void UnaRegolaCondizionaleTuttaDentroLeRigheEliminateSparisce()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];

        sheet.ConditionalRules.Add(new Formatting.ConditionalRule
        {
            Range = new CellRange(new CellAddress(2, 0), new CellAddress(3, 0)),
            Operands = ["0"],
        });

        Structure.DeleteRows(engine, 0, 2, 2);

        Assert.Empty(sheet.ConditionalRules);
    }

    [Fact]
    public void UnaFormulaSuUnAltroFoglioSiAggiornaSoloSeParlaDelFoglioToccato()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var other = workbook.AddSheet("Altro");

        engine.SetValue(Cell(4), CellValue.Number(3));
        other.SetValue(new CellAddress(0, 0), CellValue.Empty);

        var otherIndex = workbook.IndexOf(other);
        engine.SetFormula(new SheetCell(otherIndex, new CellAddress(0, 0)), "=Sheet1.A5");
        engine.SetFormula(new SheetCell(otherIndex, new CellAddress(1, 0)), "=A5");

        Structure.InsertRows(engine, 0, 0, 1);

        Assert.Equal("=Sheet1.A6", other[new CellAddress(0, 0)].Formula);
        Assert.Equal("=A5", other[new CellAddress(1, 0)].Formula);
    }

    [Fact]
    public void AnnullareConLIstantaneaRipristinaAncheLeFormuleDegliAltriFogliEICommenti()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var first = workbook.Sheets[0];
        var second = workbook.AddSheet("Foglio2");
        Assert.True(CellAddress.TryParseA1("A10", out var a10));
        Assert.True(CellAddress.TryParseA1("A1", out var a1));
        first.SetValue(a10, Values.CellValue.Number(7));
        second.SetFormula(a1, "=" + first.Name + ".A10");
        first.Comments[a10] = new CellComment { Text = "nota", Author = "autore" };
        engine.RecalculateAll();

        var snapshots = new SheetSnapshots();
        var before = snapshots.Capture(first);
        new SheetStructure().DeleteRows(engine, 0, 1, 2);
        snapshots.Restore(engine, 0, before);

        Assert.Equal("=" + first.Name + ".A10", second[a1].Formula);
        Assert.True(first.Comments.ContainsKey(a10));
        Assert.Equal(7, second[a1].Value.AsNumber());
    }

    [Fact]
    public void LaRigaInseritaPrendeMisuraEModoDellaRigaSopra()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var layout = workbook.Sheets[0].Layout;
        layout.DefaultRowHeightManual = true;
        layout.MakeRowOptimal(1);
        layout.MakeRowOptimal(4);
        layout.SetRowHeight(5, Twip.FromPoints(40));

        Structure.InsertRows(engine, 0, 2, 1);
        Structure.InsertRows(engine, 0, 7, 1);

        Assert.True(layout.IsRowAutomatic(2));
        Assert.True(layout.IsRowAutomatic(5));
        Assert.False(layout.IsRowAutomatic(0));
        Assert.Equal(40, layout.RowHeight(6).ToPoints(), 1);
        Assert.Equal(40, layout.RowHeight(7).ToPoints(), 1);
        Assert.True(layout.IsRowHeightManual(7));
    }
}
