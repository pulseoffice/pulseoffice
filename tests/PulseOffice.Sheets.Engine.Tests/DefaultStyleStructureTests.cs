using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class DefaultStyleStructureTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    [Fact]
    public void InserireUnaColonnaSpostaLoStileDellaColonna()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Sheets[0].Layout.SetColumnStyle(2, 5);

        new SheetStructure().InsertColumns(engine, 0, 0, 1);

        Assert.Null(workbook.Sheets[0].Layout.ColumnStyle(2));
        Assert.Equal(5, workbook.Sheets[0].Layout.ColumnStyle(3));
    }

    [Fact]
    public void EliminareUnaRigaToglieIlSuoStile()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        workbook.Sheets[0].Layout.SetRowStyle(1, 5);
        workbook.Sheets[0].Layout.SetRowStyle(4, 6);

        new SheetStructure().DeleteRows(engine, 0, 1, 1);

        Assert.Equal(6, workbook.Sheets[0].Layout.RowStyle(3));
        Assert.Single(workbook.Sheets[0].Layout.RowStyles);
    }

    [Fact]
    public void LIstantaneaRipristinaGliStiliDiColonnaEDiRiga()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.Layout.SetColumnStyle(1, 5);
        var snapshots = new SheetSnapshots();
        var snapshot = snapshots.Capture(sheet);

        sheet.Layout.SetColumnStyle(1, null);
        sheet.Layout.SetRowStyle(3, 7);
        snapshots.Restore(engine, 0, snapshot);

        Assert.Equal(5, sheet.Layout.ColumnStyle(1));
        Assert.Empty(sheet.Layout.RowStyles);
    }

    [Fact]
    public void UnaColonnaInseritaPrendeIlFormatoDiQuellaASinistra()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.Layout.SetColumnStyle(1, 5);
        sheet.SetStyle(new Addressing.CellAddress(4, 1), 8);
        sheet.SetStyle(new Addressing.CellAddress(6, 1), Styling.StyleCatalog.DefaultStyleId);

        new SheetStructure().InsertColumns(engine, 0, 2, 2);

        Assert.Equal(5, sheet.Layout.ColumnStyle(2));
        Assert.Equal(5, sheet.Layout.ColumnStyle(3));
        Assert.Equal(8, sheet.StyleIdAt(new Addressing.CellAddress(4, 3)));
        Assert.Equal(Styling.StyleCatalog.DefaultStyleId, sheet.StyleIdAt(new Addressing.CellAddress(6, 2)));
        Assert.Equal(5, sheet.StyleIdAt(new Addressing.CellAddress(9, 3)));
        Assert.Null(sheet.Layout.ColumnStyle(4));
    }

    [Fact]
    public void UnaRigaInseritaPrendeIlFormatoDiQuellaSopra()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.Layout.SetRowStyle(2, 6);
        sheet.SetStyle(new Addressing.CellAddress(2, 3), 9);
        sheet.SetValue(new Addressing.CellAddress(3, 0), Values.CellValue.Number(1));

        new SheetStructure().InsertRows(engine, 0, 3, 1);

        Assert.Equal(6, sheet.Layout.RowStyle(3));
        Assert.Equal(9, sheet.StyleIdAt(new Addressing.CellAddress(3, 3)));
        Assert.Null(sheet.Layout.RowStyle(4));
        Assert.Equal(Values.CellValue.Number(1), sheet.GetValue(new Addressing.CellAddress(4, 0)));
        Assert.Equal(Styling.StyleCatalog.DefaultStyleId, sheet.StyleIdAt(new Addressing.CellAddress(4, 0)));
    }

    [Fact]
    public void InserireInTestaNonCopiaNulla()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.Layout.SetRowStyle(0, 6);

        new SheetStructure().InsertRows(engine, 0, 0, 1);

        Assert.Null(sheet.Layout.RowStyle(0));
        Assert.Equal(6, sheet.Layout.RowStyle(1));
    }

    [Fact]
    public void LIstantaneaConservaLaCellaSempliceEsplicita()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet.Layout.SetColumnStyle(1, 5);
        var address = new Addressing.CellAddress(3, 1);
        sheet.SetStyle(address, Styling.StyleCatalog.DefaultStyleId);
        var snapshots = new SheetSnapshots();
        var snapshot = snapshots.Capture(sheet);

        new SheetStructure().InsertColumns(engine, 0, 2, 1);
        snapshots.Restore(engine, 0, snapshot);

        Assert.Equal(Styling.StyleCatalog.DefaultStyleId, sheet.StyleIdAt(address));
        Assert.Null(sheet.Layout.ColumnStyle(2));
    }
}
