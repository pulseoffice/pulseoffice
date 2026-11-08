using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class IntegrationFunctionTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private sealed class FixedWeb(string? content) : IWebResourceAccess
    {
        public string? Fetch(string url) => content;
    }

    private sealed class FixedPivot(CellValue? value) : IPivotDataSource
    {
        public IReadOnlyList<(string Field, string Item)> LastFilters { get; private set; } = [];

        public CellValue? Lookup(
            int sheetIndex, CellAddress target, string dataField,
            IReadOnlyList<(string Field, string Item)> filters)
        {
            LastFilters = filters;
            return value;
        }
    }

    private static SheetCell A(int row) => new(0, new CellAddress(row, 0));

    private static SheetCell At(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    [Fact]
    public void ColoreComponeIlNumeroDelColore()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetFormula(A(0), "=COLOR(255;128;0)");

        Assert.Equal(0xFF8000, engine.GetValue(A(0)).AsNumber());
    }

    [Fact]
    public void ColoreRifiutaIValoriFuoriScala()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetFormula(A(0), "=COLOR(300;-10;0)");

        Assert.Equal(0xFF0000, engine.GetValue(A(0)).AsNumber());
    }

    [Fact]
    public void StileChiedeLoStileEnonCambiaIlValore()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetFormula(A(0), "=STYLE(\"Buono\")");

        Assert.Equal(0, engine.GetValue(A(0)).AsNumber());
        Assert.Equal("Buono", engine.RequestedStyles[A(0)]);
    }

    [Fact]
    public void SenzaAccessoDiReteWebserviceDaErrore()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetFormula(A(0), "=WEBSERVICE(\"https://esempio.invalido/dati\")");

        Assert.True(engine.GetValue(A(0)).IsError);
    }

    [Fact]
    public void ConLAccessoConcessoWebserviceRestituisceIlContenuto()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.Web = new FixedWeb("<r><v>7</v></r>");

        engine.SetFormula(A(0), "=WEBSERVICE(\"https://esempio.invalido/dati\")");

        Assert.Equal("<r><v>7</v></r>", engine.GetValue(A(0)).ToString());
    }

    [Fact]
    public void FilterxmlLeggeUnValoreDalDocumento()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(A(0), CellValue.Text("<listino><prezzo>12,5</prezzo></listino>"));
        engine.SetFormula(A(1), "=FILTERXML(A1;\"//prezzo\")");

        Assert.Equal("12,5", engine.GetValue(A(1)).ToString());
    }

    [Fact]
    public void FilterxmlSuUnDocumentoRottoDaErrore()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetFormula(A(0), "=FILTERXML(\"non xml\";\"//a\")");

        Assert.True(engine.GetValue(A(0)).IsError);
    }

    [Fact]
    public void MultirangeUnisceIntervalliLontani()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(At("A1"), CellValue.Number(1));
        engine.SetValue(At("A2"), CellValue.Number(2));
        engine.SetValue(At("C1"), CellValue.Number(10));
        engine.SetValue(At("C2"), CellValue.Number(20));

        engine.SetFormula(At("E1"), "=SUM(MULTIRANGE(A1:A2;C1:C2))");

        Assert.Equal(33, engine.GetValue(At("E1")).AsNumber());
    }

    [Fact]
    public void GetpivotdataChiedeAllaTabellaPivot()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.Pivots = new FixedPivot(CellValue.Number(220));

        engine.SetFormula(At("A1"), "=GETPIVOTDATA(\"Totale\";B10;\"Zona\";\"Nord\")");

        Assert.Equal(220, engine.GetValue(At("A1")).AsNumber());
    }

    [Fact]
    public void GetpivotdataNellaFormaEsternoLeggeCampiEdElementiDallaStringa()
    {
        var (_, engine) = fixture.CreateWorkbook();
        var pivot = new FixedPivot(CellValue.Number(21));
        engine.Pivots = pivot;

        engine.SetFormula(At("A1"), "=GETPIVOTDATA(I30;\"Name['Wendy   '] Type[F]\")");

        Assert.Equal(21, engine.GetValue(At("A1")).AsNumber());
        Assert.Equal([("Name", "Wendy   "), ("Type", "F")], pivot.LastFilters);
    }

    [Fact]
    public void SenzaTabellePivotGetpivotdataDaErroreDiRiferimento()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetFormula(At("A1"), "=GETPIVOTDATA(\"Totale\";B10)");

        Assert.Equal(CellError.Reference, engine.GetValue(At("A1")).Error);
    }

    [Fact]
    public void OperazioniMultipleSostituisconoLIngresso()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(At("A1"), CellValue.Number(2));
        engine.SetFormula(At("B1"), "=A1*10");
        engine.SetValue(At("A3"), CellValue.Number(7));

        engine.SetFormula(At("C3"), "=MULTIPLE.OPERATIONS(B1;A1;A3)");

        Assert.Equal(70, engine.GetValue(At("C3")).AsNumber());
        Assert.Equal(20, engine.GetValue(At("B1")).AsNumber());
    }

    [Fact]
    public void OperazioniMultipleSeguonoLaCatenaDelleFormule()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(At("A1"), CellValue.Number(2));
        engine.SetFormula(At("B1"), "=A1*10");
        engine.SetFormula(At("C1"), "=B1+5");
        engine.SetValue(At("A5"), CellValue.Number(3));

        engine.SetFormula(At("D5"), "=MULTIPLE.OPERATIONS(C1;A1;A5)");

        Assert.Equal(35, engine.GetValue(At("D5")).AsNumber());
    }

    [Fact]
    public void OperazioniMultipleIncrocianoDueIngressi()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(At("A1"), CellValue.Number(1));
        engine.SetValue(At("A2"), CellValue.Number(1));
        engine.SetFormula(At("B1"), "=A1*A2");

        engine.SetValue(At("A6"), CellValue.Number(4));
        engine.SetValue(At("B6"), CellValue.Number(5));

        engine.SetFormula(At("C6"), "=MULTIPLE.OPERATIONS(B1;A1;A6;A2;B6)");

        Assert.Equal(20, engine.GetValue(At("C6")).AsNumber());
    }

    [Fact]
    public void GoalseekTrovaIlValoreCercato()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(At("A1"), CellValue.Number(1));
        engine.SetFormula(At("B1"), "=A1*3+2");

        engine.SetFormula(At("C1"), "=GOALSEEK(B1;A1;20)");

        Assert.Equal(6, engine.GetValue(At("C1")).AsNumber(), 4);
    }

    [Fact]
    public void GoalseekSuUnaFormulaSenzaSoluzioneDaNonDisponibile()
    {
        var (_, engine) = fixture.CreateWorkbook();

        engine.SetValue(At("A1"), CellValue.Number(1));
        engine.SetFormula(At("B1"), "=5");

        engine.SetFormula(At("C1"), "=GOALSEEK(B1;A1;20)");

        Assert.Equal(CellError.NotAvailable, engine.GetValue(At("C1")).Error);
    }
}
