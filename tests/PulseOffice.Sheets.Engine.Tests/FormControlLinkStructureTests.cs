using PulseOffice.Document.Forms;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class FormControlLinkStructureTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly ISheetStructure Structure = new SheetStructure();

    private static PlacedObject Control(string linked, string source) => new()
    {
        Id = "spunta",
        Anchor = new CellAddress(0, 5),
        Content = new FormControlObject
        {
            Id = "spunta",
            ControlKind = FormControlKind.ListBox,
            Properties = new FormControlProperties { LinkedCell = linked, SourceRange = source },
        },
    };

    private static FormControlProperties Properties(Worksheet sheet) =>
        ((FormControlObject)sheet.Objects[0].Content).Properties;

    [Fact]
    public void InserendoRigheLaCellaCollegataELIntervalloSeguono()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Sheets[0].Objects.Add(Control("A3", "B2:B4"));

        Structure.InsertRows(engine, 0, 0, 2);

        Assert.Equal("A5", Properties(workbook.Sheets[0]).LinkedCell);
        Assert.Equal("B4:B6", Properties(workbook.Sheets[0]).SourceRange);
    }

    [Fact]
    public void EliminandoLaCellaCollegataIlCollegamentoSiToglie()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Sheets[0].Objects.Add(Control("A3", "B2:B4"));

        Structure.DeleteRows(engine, 0, 2, 1);

        Assert.Null(Properties(workbook.Sheets[0]).LinkedCell);
        Assert.Equal("B2:B3", Properties(workbook.Sheets[0]).SourceRange);
    }

    [Fact]
    public void SpostandoCelleInBassoSiAggiornaSoloIlCollegamentoNellaBanda()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Sheets[0].Objects.Add(Control("A3", "A1:A4"));
        workbook.Sheets[0].Objects.Add(Control("B3", "B1:B4") with { Id = "altro" });

        new SheetCellShift(Structure).Insert(engine, 0, new CellRange(new CellAddress(1, 0), new CellAddress(1, 0)), CellShift.Down);

        Assert.Equal("A4", Properties(workbook.Sheets[0]).LinkedCell);
        Assert.Equal("A1:A5", Properties(workbook.Sheets[0]).SourceRange);
        var other = ((FormControlObject)workbook.Sheets[0].Objects[1].Content).Properties;
        Assert.Equal("B3", other.LinkedCell);
        Assert.Equal("B1:B4", other.SourceRange);
    }

    [Fact]
    public void InserendoRigheInUnAltroFoglioIlCollegamentoQualificatoSegue()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.AddSheet("Altro foglio");
        workbook.Sheets[0].Objects.Add(Control("'Altro foglio'!B2", "'Altro foglio'!C1:C3"));

        Structure.InsertRows(engine, 1, 0, 2);

        Assert.Equal("'Altro foglio'!B4", Properties(workbook.Sheets[0]).LinkedCell);
        Assert.Equal("'Altro foglio'!C3:C5", Properties(workbook.Sheets[0]).SourceRange);
    }

    [Fact]
    public void InserendoColonneNelFoglioDelControlloIlCollegamentoAdAltroFoglioResta()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.AddSheet("Altro");
        workbook.Sheets[0].Objects.Add(Control("Altro!B2", "C1:C3"));

        Structure.InsertColumns(engine, 0, 0, 1);

        Assert.Equal("Altro!B2", Properties(workbook.Sheets[0]).LinkedCell);
        Assert.Equal("D1:D3", Properties(workbook.Sheets[0]).SourceRange);
    }

    [Fact]
    public void EliminandoColonneNellAltroFoglioIlCollegamentoSiSpostaOSiToglie()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.AddSheet("Altro");
        workbook.Sheets[0].Objects.Add(Control("Altro!C2", "Altro!A1:A3"));

        Structure.DeleteColumns(engine, 1, 0, 1);

        Assert.Equal("Altro!B2", Properties(workbook.Sheets[0]).LinkedCell);
        Assert.Null(Properties(workbook.Sheets[0]).SourceRange);
    }

    [Fact]
    public void SpostandoCelleInUnAltroFoglioIlCollegamentoQualificatoSegue()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.AddSheet("Altro");
        workbook.Sheets[0].Objects.Add(Control("Altro!A3", "A1:A4"));

        new SheetCellShift(Structure).Insert(engine, 1, new CellRange(new CellAddress(1, 0), new CellAddress(1, 0)), CellShift.Down);

        Assert.Equal("Altro!A4", Properties(workbook.Sheets[0]).LinkedCell);
        Assert.Equal("A1:A4", Properties(workbook.Sheets[0]).SourceRange);
    }
}
