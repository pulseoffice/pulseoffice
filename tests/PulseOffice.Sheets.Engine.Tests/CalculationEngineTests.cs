using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class CalculationEngineTests : IClassFixture<EngineFixture>
{
    private readonly EngineFixture _fixture;

    public CalculationEngineTests(EngineFixture fixture) => _fixture = fixture;

    private (Workbook Workbook, CalculationEngine Engine) Nuovo() => _fixture.CreateWorkbook();

    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    [Theory]
    [InlineData("=1+2", 3)]
    [InlineData("=2*3+4", 10)]
    [InlineData("=2^10", 1024)]
    [InlineData("=10/4", 2.5)]
    [InlineData("=50%", 0.5)]
    [InlineData("=-3+5", 2)]
    [InlineData("=SUM(1;2;3;4)", 10)]
    [InlineData("=AVERAGE(2;4;6)", 4)]
    [InlineData("=MOD(-3;2)", 1)]
    [InlineData("=ROUND(2.5;0)", 3)]
    [InlineData("=ROUND(-2.5;0)", -3)]
    public void CalcolaEspressioni(string formula, double atteso)
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), formula);
        Assert.Equal(atteso, engine.GetValue(C("A1")).AsNumber(), 10);
    }

    [Fact]
    public void PropagaLeModificheAValle()
    {
        var (_, engine) = Nuovo();
        engine.SetValue(C("A1"), CellValue.Number(10));
        engine.SetFormula(C("B1"), "=A1*2");
        engine.SetFormula(C("C1"), "=B1+5");

        Assert.Equal(25, engine.GetValue(C("C1")).AsNumber());

        engine.SetValue(C("A1"), CellValue.Number(100));
        Assert.Equal(200, engine.GetValue(C("B1")).AsNumber());
        Assert.Equal(205, engine.GetValue(C("C1")).AsNumber());
    }

    [Fact]
    public void RispettaLOrdineTopologicoAncheAllIndietro()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("C1"), "=B1+1");
        engine.SetFormula(C("B1"), "=A1+1");
        engine.SetValue(C("A1"), CellValue.Number(1));

        Assert.Equal(3, engine.GetValue(C("C1")).AsNumber());
    }

    [Fact]
    public void AggiornaAttraversoUnIntervallo()
    {
        var (_, engine) = Nuovo();
        for (var r = 0; r < 5; r++)
            engine.SetValue(new SheetCell(0, new CellAddress(r, 0)), CellValue.Number(r + 1));

        engine.SetFormula(C("B1"), "=SUM(A1:A5)");
        Assert.Equal(15, engine.GetValue(C("B1")).AsNumber());

        engine.SetValue(C("A3"), CellValue.Number(30));
        Assert.Equal(42, engine.GetValue(C("B1")).AsNumber());
    }

    [Fact]
    public void RilevaIRiferimentiCircolari()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=B1+1");
        engine.SetFormula(C("B1"), "=A1+1");

        Assert.True(engine.GetValue(C("A1")).IsError);
        Assert.Equal(CellError.Circular, engine.GetValue(C("A1")).Error);
        Assert.Contains(C("A1"), engine.CircularCells);
        Assert.Contains(C("B1"), engine.CircularCells);
    }

    [Fact]
    public void RilevaLAutoriferimento()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=A1+1");
        Assert.Equal(CellError.Circular, engine.GetValue(C("A1")).Error);
    }

    [Theory]
    [InlineData("=1/0", CellError.DivideByZero)]
    [InlineData("=SQRT(-1)", CellError.Number)]
    [InlineData("=NONESISTE(1)", CellError.Name)]
    [InlineData("=NA()", CellError.NotAvailable)]
    [InlineData("=1+\"ciao\"", CellError.Value)]
    public void ProduceGliErroriGiusti(string formula, CellError atteso)
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), formula);
        var value = engine.GetValue(C("A1"));
        Assert.True(value.IsError, $"atteso errore, ottenuto {value}");
        Assert.Equal(atteso, value.Error);
    }

    [Fact]
    public void GliErroriSiPropaganoAValle()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=1/0");
        engine.SetFormula(C("B1"), "=A1+1");
        Assert.Equal(CellError.DivideByZero, engine.GetValue(C("B1")).Error);
    }

    [Fact]
    public void SeErroreIntercettaInveceDiPropagare()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=1/0");
        engine.SetFormula(C("B1"), "=IFERROR(A1;\"riparato\")");
        Assert.Equal("riparato", engine.GetValue(C("B1")).AsText());
    }

    [Fact]
    public void RisolveIRiferimentiFraFogli()
    {
        var (wb, engine) = Nuovo();
        var secondo = wb.AddSheet("Dati");
        secondo.SetValue(new CellAddress(0, 0), CellValue.Number(42));

        engine.SetFormula(C("A1"), "=Dati.A1*2");
        Assert.Equal(84, engine.GetValue(C("A1")).AsNumber());
    }

    [Fact]
    public void UnFoglioInesistenteEUnRiferimentoRotto()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=Fantasma.A1");
        Assert.Equal(CellError.Reference, engine.GetValue(C("A1")).Error);
    }

    [Fact]
    public void RisolveINomiDefiniti()
    {
        var (wb, engine) = Nuovo();
        wb.DefineName(new DefinedName("Aliquota", "0.22"));
        engine.SetValue(C("A1"), CellValue.Number(100));
        engine.SetFormula(C("B1"), "=A1*Aliquota");

        Assert.Equal(22, engine.GetValue(C("B1")).AsNumber(), 10);
    }

    [Fact]
    public void UnNomeRelativoSiSpostaConLaCellaCheLoUsa()
    {
        var (wb, engine) = Nuovo();
        wb.DefineName(new DefinedName("AccantoASinistra", "XFD1"));
        engine.SetValue(C("A3"), CellValue.Number(5));
        engine.SetFormula(C("B3"), "=AccantoASinistra*2");
        Assert.Equal(10, engine.GetValue(C("B3")).AsNumber(), 10);

        engine.SetValue(C("A3"), CellValue.Number(7));
        Assert.Equal(14, engine.GetValue(C("B3")).AsNumber(), 10);
    }

    [Fact]
    public void UnaFormulaMalformataRestaNellaCella()
    {
        var (wb, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=1+*2");

        Assert.Equal("=1+*2", wb.Sheets[0][new CellAddress(0, 0)].Formula);
        Assert.Equal(CellError.Name, engine.GetValue(C("A1")).Error);
    }

    [Fact]
    public void RicalcolaTuttoDopoUnCaricamento()
    {
        var wb = Workbook.CreateDefault();
        var sheet = wb.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(7));
        sheet.SetFormula(new CellAddress(1, 0), "=A1*3");
        sheet.SetFormula(new CellAddress(2, 0), "=A2+1");

        var engine = _fixture.CreateEngine(wb);
        engine.RecalculateAll();

        Assert.Equal(21, engine.GetValue(new SheetCell(0, new CellAddress(1, 0))).AsNumber());
        Assert.Equal(22, engine.GetValue(new SheetCell(0, new CellAddress(2, 0))).AsNumber());
    }

    [Fact]
    public void IlValoreCalcolatoTornaNelModello()
    {
        var (wb, engine) = Nuovo();
        engine.SetValue(C("A1"), CellValue.Number(5));
        engine.SetFormula(C("B1"), "=A1*2");

        Assert.Equal(10, wb.Sheets[0][new CellAddress(0, 1)].Value.AsNumber());
    }

    [Fact]
    public void ConfrontaTestoSenzaDistinguereMaiuscole()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("A1"), "=\"Ciao\"=\"CIAO\"");
        Assert.True(engine.GetValue(C("A1")).AsBoolean());
    }

    [Fact]
    public void IlTestoNegliIntervalliNonPartecipaAllaSomma()
    {
        var (_, engine) = Nuovo();
        engine.SetValue(C("A1"), CellValue.Number(10));
        engine.SetValue(C("A2"), CellValue.Text("non un numero"));
        engine.SetValue(C("A3"), CellValue.Number(5));

        engine.SetFormula(C("B1"), "=SUM(A1:A3)");
        Assert.Equal(15, engine.GetValue(C("B1")).AsNumber());

        engine.SetFormula(C("B2"), "=COUNTA(A1:A3)");
        Assert.Equal(3, engine.GetValue(C("B2")).AsNumber());

        engine.SetFormula(C("B3"), "=COUNT(A1:A3)");
        Assert.Equal(2, engine.GetValue(C("B3")).AsNumber());
    }

    [Fact]
    public void UnaCellaCitataSiaDirettamenteSiaInUnIntervalloNonECircolare()
    {
        var (_, engine) = Nuovo();
        engine.SetFormula(C("B1"), "=A1+SUM(A1:A3)");
        engine.SetFormula(C("A1"), "=1");
        engine.RecalculateAll();

        Assert.Equal(2, engine.GetValue(C("B1")).AsNumber());
    }

    [Fact]
    public void UnaScritturaFattaDirettamenteNelFoglioRicalcolaLeDipendenti()
    {
        var (workbook, engine) = Nuovo();
        engine.SetFormula(C("C1"), "=SUM(A1:A3)");
        engine.SetFormula(C("D1"), "=C1*2");

        Assert.True(CellAddress.TryParseA1("A1", out var a1));
        Assert.True(CellAddress.TryParseA1("B1", out var b1));
        workbook.Sheets[0][a1] = workbook.Sheets[0][a1] with { Value = CellValue.Number(10) };
        workbook.Sheets[0][b1] = workbook.Sheets[0][b1] with { Formula = "=A1*3" };

        Assert.Equal(10, engine.GetValue(C("C1")).AsNumber());
        Assert.Equal(20, engine.GetValue(C("D1")).AsNumber());
        Assert.Equal(30, engine.GetValue(C("B1")).AsNumber());
    }

    [Fact]
    public void CambiareUnNomeRicalcolaLeFormuleCheLoUsano()
    {
        var (workbook, engine) = Nuovo();
        workbook.DefineName(new DefinedName("Aliquota", "0.2"));
        engine.SetFormula(C("A1"), "=Aliquota*100");

        workbook.DefineName(new DefinedName("Aliquota", "0.3"));

        Assert.Equal(30, engine.GetValue(C("A1")).AsNumber(), 6);
    }
}
