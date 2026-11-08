using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class TrackedChangesTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static SheetCell Cell(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return new SheetCell(0, address);
    }

    [Fact]
    public void SenzaRegistrazioneNonSiSegnaNulla()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell("A1"), CellValue.Number(1));

        Assert.Empty(workbook.Changes.Changes);
    }

    [Fact]
    public void UnaModificaRegistrataConservaIlValoreDiPrima()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell("A1"), CellValue.Number(1));
        workbook.Changes.IsRecording = true;
        engine.SetValue(Cell("A1"), CellValue.Number(2));

        var change = Assert.Single(workbook.Changes.Changes);

        Assert.Equal(1d, change.Before.AsNumber());
        Assert.Equal(2d, change.After.AsNumber());
        Assert.Equal(ChangeState.Pending, change.State);
    }

    [Fact]
    public void ScrivereLoStessoValoreNonEUnaModifica()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        engine.SetValue(Cell("A1"), CellValue.Number(1));
        workbook.Changes.IsRecording = true;
        engine.SetValue(Cell("A1"), CellValue.Number(1));

        Assert.Empty(workbook.Changes.Changes);
    }

    [Fact]
    public void AccettareUnaModificaLaTogliDaQuellePendenti()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        workbook.Changes.IsRecording = true;
        engine.SetValue(Cell("A1"), CellValue.Number(5));

        var change = Assert.Single(workbook.Changes.Changes);

        Assert.True(workbook.Changes.Resolve(change.Id, ChangeState.Accepted));
        Assert.Equal(0, workbook.Changes.PendingCount);
    }

    [Fact]
    public void UnaFormulaRegistraLaFormulaDiPrimaEQuellaDopo()
    {
        var (workbook, engine) = fixture.CreateWorkbook();

        engine.SetFormula(Cell("A1"), "=1+1");
        workbook.Changes.IsRecording = true;
        engine.SetFormula(Cell("A1"), "=2+2");

        var change = Assert.Single(workbook.Changes.Changes);

        Assert.Equal("=1+1", change.FormulaBefore);
        Assert.Equal("=2+2", change.FormulaAfter);
    }
}
