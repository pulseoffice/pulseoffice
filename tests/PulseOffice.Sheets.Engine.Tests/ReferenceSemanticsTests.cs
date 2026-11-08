using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class ReferenceSemanticsTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    [Theory]
    [InlineData("I5", "=INDEX(I1:I10;2)", "5")]
    [InlineData("J5", "=SUM(J1:J4)+INDEX(J1:J10;1)", "0")]
    [InlineData("K5", "=ROWS(K1:K10)", "10")]
    [InlineData("K5", "=COLUMNS(A5:Z5)", "26")]
    [InlineData("L5", "=VLOOKUP(5;I1:L10;1;0)", "5")]
    [InlineData("M5", "=SUM(M1:M10)", "#VALUE!")]
    [InlineData("N5", "=N5+1", "#VALUE!")]
    public void Un_intervallo_che_contiene_la_cella_e_circolare_solo_se_la_cella_si_legge(string cell, string formula, string atteso)
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(C("I2"), CellValue.Number(5));
        engine.SetFormula(C(cell), formula);
        Assert.Equal(atteso, engine.GetValue(C(cell)).ToString());
        Assert.Equal(atteso.StartsWith('#'), engine.CircularCells.Contains(C(cell)));
    }

    [Fact]
    public void La_formula_con_l_intervallo_che_la_contiene_segue_le_modifiche()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(C("I2"), CellValue.Number(5));
        engine.SetFormula(C("I5"), "=INDEX(I1:I10;2)*2");
        engine.SetValue(C("I2"), CellValue.Number(7));
        Assert.Equal(14, engine.GetValue(C("I5")).AsNumber());
    }

    [Fact]
    public void ROW_e_COLUMN_senza_argomenti_danno_la_posizione_di_ogni_cella_della_formula_matrice()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetArrayFormula(C("D1"), R("D1:D3"), "=ROW()");
        engine.SetArrayFormula(C("E1"), R("E1:G1"), "=COLUMN()*10");
        Assert.Equal([1d, 2, 3], new[] { "D1", "D2", "D3" }.Select(c => engine.GetValue(C(c)).AsNumber()));
        Assert.Equal([50d, 60, 70], new[] { "E1", "F1", "G1" }.Select(c => engine.GetValue(C(c)).AsNumber()));
    }

    [Theory]
    [InlineData("=INDEX(A1:A3~B1:B3;2;1;2)", 20)]
    [InlineData("=INDEX(A1:A3~B1:B3;3;1;1)", 3)]
    [InlineData("=SUM(INDEX(A1:B3;0;2))", 60)]
    public void INDEX_sceglie_l_area_di_una_lista_di_riferimenti(string formula, double atteso)
    {
        var (_, engine) = fixture.CreateWorkbook();
        for (var row = 1; row <= 3; row++)
        {
            engine.SetValue(C($"A{row}"), CellValue.Number(row));
            engine.SetValue(C($"B{row}"), CellValue.Number(row * 10));
        }
        engine.SetFormula(C("H1"), formula);
        Assert.Equal(atteso, engine.GetValue(C("H1")).AsNumber());
    }

    [Fact]
    public void INDEX_con_area_fuori_dalla_lista_da_errore_di_riferimento()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(C("H1"), "=INDEX(A1:A3~B1:B3;1;1;3)");
        Assert.Equal(CellError.Reference, engine.GetValue(C("H1")).Error);
    }
}
