using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class SingleValueOperatorTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private SheetCell C(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    [Fact]
    public void LaChiocciolaIntersecaLIntervalloConLaRigaDellaFormula()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(C("A1"), CellValue.Number(10));
        engine.SetValue(C("A2"), CellValue.Number(20));
        engine.SetValue(C("A3"), CellValue.Number(30));
        engine.SetFormula(C("C2"), "=@A1:A3");

        Assert.Equal(20, engine.GetValue(C("C2")).AsNumber());
    }

    [Fact]
    public void LaChiocciolaFuoriDallIntervalloDaValore()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetValue(C("A1"), CellValue.Number(10));
        engine.SetValue(C("A2"), CellValue.Number(20));
        engine.SetFormula(C("C5"), "=@A1:A2");

        Assert.Equal(CellError.Value, engine.GetValue(C("C5")).Error);
    }

    [Fact]
    public void LaChiocciolaSuUnaMatricePrendeIlPrimoElemento()
    {
        var (_, engine) = fixture.CreateWorkbook();
        engine.SetFormula(C("B1"), "=@SEQUENCE(3)");

        Assert.Equal(1, engine.GetValue(C("B1")).AsNumber());
    }
}
