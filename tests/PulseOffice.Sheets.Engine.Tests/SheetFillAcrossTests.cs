using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class SheetFillAcrossTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    private (Workbook Workbook, Recalculation.CalculationEngine Engine) Cartella()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        while (workbook.Sheets.Count < 2) workbook.AddSheet($"Sheet{workbook.Sheets.Count + 1}");

        var source = workbook.Sheets[0];
        source.SetValue(A("A1"), CellValue.Number(2));
        source.SetValue(A("A2"), CellValue.Text("totale"));
        source.SetFormula(A("A3"), "=A1*10");
        source.SetOwnStyle(A("A1"), 7);

        var target = workbook.Sheets[1];
        target.SetValue(A("A1"), CellValue.Number(5));
        target.SetValue(A("A4"), CellValue.Text("resta"));

        engine.RecalculateAll();
        return (workbook, engine);
    }

    private static CellRange Area()
    {
        Assert.True(CellRange.TryParseA1("A1:A4", out var range));
        return range;
    }

    [Fact]
    public void TuttoCopiaFormuleAllaStessaPosizioneEFormati()
    {
        var (workbook, engine) = Cartella();

        new SheetFillAcross().Fill(engine, workbook, 0, Area(), [1], FillAcrossOptions.All);
        engine.RecalculateAll();

        var target = workbook.Sheets[1];
        Assert.Equal("=A1*10", target[A("A3")].Formula);
        Assert.Equal(20, target.GetValue(A("A3")).AsNumber());
        Assert.Equal("totale", target.GetValue(A("A2")).AsText());
        Assert.Equal(7, target.OwnStyleAt(A("A1")));
        Assert.True(target.GetValue(A("A4")).IsEmpty);
    }

    [Fact]
    public void SoloFormuleLasciaNumeriETesti()
    {
        var (workbook, engine) = Cartella();

        new SheetFillAcross().Fill(engine, workbook, 0, Area(), [1], new FillAcrossOptions(FillAcrossContent.Formulas));
        engine.RecalculateAll();

        var target = workbook.Sheets[1];
        Assert.Equal(5, target.GetValue(A("A1")).AsNumber());
        Assert.Equal(50, target.GetValue(A("A3")).AsNumber());
        Assert.Equal("resta", target.GetValue(A("A4")).AsText());
        Assert.Null(target.OwnStyleAt(A("A1")));
    }

    [Fact]
    public void SaltaLeVuoteConservaIlContenutoEsistente()
    {
        var (workbook, engine) = Cartella();

        new SheetFillAcross().Fill(engine, workbook, 0, Area(), [1], new FillAcrossOptions(FillAcrossContent.All, SkipEmpty: true));

        Assert.Equal("resta", workbook.Sheets[1].GetValue(A("A4")).AsText());
    }

    [Fact]
    public void IFogliProtettiNonSiRiempionoEUnaColonnaInteraNonSiScorre()
    {
        var (workbook, engine) = Cartella();
        workbook.Sheets[1].Protection = new SheetProtection { IsEnabled = true };

        Assert.Equal(0, new SheetFillAcross().Fill(engine, workbook, 0, new CellRange(A("A1"), new CellAddress(CellAddress.MaxRows - 1, 0)), [1], FillAcrossOptions.All));
        Assert.Equal(5, workbook.Sheets[1].GetValue(A("A1")).AsNumber());
    }

    [Fact]
    public void ICollegamentiEsterniScendonoInserendoRighe()
    {
        var (workbook, engine) = Cartella();
        workbook.ExternalData.Replace(new AreaLink { SheetIndex = 0, Anchor = A("C5"), Url = "a.csv", Rows = 3, Columns = 2 });
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Data1", SheetIndex = 0, Range = new CellRange(A("E1"), A("F2")) });

        new SheetStructure().InsertRows(engine, 0, 0, 2);

        Assert.Equal(A("C7"), workbook.ExternalData.AreaLinks[0].Anchor);
        Assert.Equal(new CellRange(A("E3"), A("F4")), workbook.DatabaseRanges.Find("Data1")!.Range);

        new SheetStructure().DeleteRows(engine, 0, 6, 3);
        Assert.Empty(workbook.ExternalData.AreaLinks);
    }
}
