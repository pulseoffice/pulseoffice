using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.UI;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class GridGeometryTests
{
    private static GridGeometry Standard() => new()
    {
        DefaultColumnWidth = 80,
        DefaultRowHeight = 20,
    };

    [Fact]
    public void SenzaPersonalizzazioniLeDistanzeSonoMultipliDelPredefinito()
    {
        var geometry = Standard();

        Assert.Equal(0, geometry.ColumnOffset(0));
        Assert.Equal(800, geometry.ColumnOffset(10));
        Assert.Equal(200, geometry.RowOffset(10));
    }

    [Fact]
    public void UnaColonnaPersonalizzataSpostaSoloQuelleSuccessive()
    {
        var geometry = Standard();
        geometry.SetColumnWidth(2, 200);

        Assert.Equal(160, geometry.ColumnOffset(2));

        Assert.Equal(160 + 200, geometry.ColumnOffset(3));
    }

    [Fact]
    public void LaRicercaPerCoordinataTrovaLaCellaGiusta()
    {
        var geometry = Standard();

        Assert.Equal(0, geometry.ColumnAt(0));
        Assert.Equal(0, geometry.ColumnAt(79));
        Assert.Equal(1, geometry.ColumnAt(80));
        Assert.Equal(12, geometry.ColumnAt(1000));
    }

    [Fact]
    public void LaRicercaPerCoordinataTieneContoDellePersonalizzazioni()
    {
        var geometry = Standard();
        geometry.SetColumnWidth(0, 300);

        Assert.Equal(0, geometry.ColumnAt(250));
        Assert.Equal(1, geometry.ColumnAt(300));
        Assert.Equal(2, geometry.ColumnAt(380));
    }

    [Fact]
    public void UnaCoordinataNegativaNonCorrispondeANessunaCella()
    {
        var geometry = Standard();
        Assert.Equal(-1, geometry.ColumnAt(-5));
        Assert.Equal(-1, geometry.RowAt(-5));
    }

    [Fact]
    public void SiDisegnanoSoloLeCelleVisibili()
    {
        var geometry = Standard();

        var visible = geometry.VisibleCells(0, 0, 800, 400);

        Assert.Equal(0, visible.FirstColumn);
        Assert.Equal(0, visible.FirstRow);
        Assert.InRange(visible.ColumnCount, 10, 13);
        Assert.InRange(visible.RowCount, 20, 23);
    }

    [Fact]
    public void ScorrendoCambianoLeCelleVisibiliMaNonLaLoroQuantita()
    {
        var geometry = Standard();

        var atTop = geometry.VisibleCells(0, 0, 800, 400);
        var scrolled = geometry.VisibleCells(0, 20_000, 800, 400);

        Assert.Equal(1000, scrolled.FirstRow);
        Assert.InRange(scrolled.RowCount, atTop.RowCount - 1, atTop.RowCount + 1);
    }

    [Fact]
    public void LaFinestraVisibileSiEstendeOltreIlBordo()
    {
        var geometry = Standard();

        var visible = geometry.VisibleCells(0, 0, 800, 400);
        Assert.True(geometry.RowOffset(visible.LastRow) >= 400 - geometry.DefaultRowHeight);
    }

    [Fact]
    public void UnaColonnaDiLarghezzaZeroENascosta()
    {
        var geometry = Standard();
        geometry.SetColumnWidth(3, 0);

        Assert.True(geometry.IsColumnHidden(3));

        Assert.Equal(geometry.ColumnOffset(3), geometry.ColumnOffset(4));
    }

    [Fact]
    public void LEstensioneSiFermaPocoOltreLAreaUsata()
    {
        var geometry = Standard();

        var used = new CellRange(new CellAddress(0, 0), new CellAddress(9, 4));
        var (width, height) = geometry.Extent(used);

        Assert.InRange(width, 5 * 80, 60 * 80);
        Assert.InRange(height, 10 * 20, 65 * 20);
    }

    [Fact]
    public void IlRettangoloDiUnaCellaCorrispondeAlleSueDistanze()
    {
        var geometry = Standard();
        geometry.SetRowHeight(5, 40);

        var (x, y, width, height) = geometry.CellBounds(new CellAddress(5, 2));

        Assert.Equal(160, x);
        Assert.Equal(100, y);
        Assert.Equal(80, width);
        Assert.Equal(40, height);
    }
}

public class SheetSelectionTests
{
    [Fact]
    public void UnoSpostamentoSempliceAzzeraLEstensione()
    {
        var selection = new SheetSelection();
        selection.MoveTo(new CellAddress(5, 5));

        Assert.True(selection.IsSingleCell);
        Assert.Equal(new CellAddress(5, 5), selection.Active);
    }

    [Fact]
    public void LAncoraRestaFermaMentreLEstremoSiMuove()
    {
        var selection = new SheetSelection();
        selection.MoveTo(new CellAddress(2, 2));

        selection.Move(3, 0, extend: true);
        selection.Move(1, 1, extend: true);

        Assert.Equal(new CellAddress(2, 2), selection.Range.Start);
        Assert.Equal(new CellAddress(6, 3), selection.Range.End);
    }

    [Fact]
    public void LaSelezioneNonEsceDaiLimitiDelFoglio()
    {
        var selection = new SheetSelection();
        selection.MoveTo(CellAddress.First);

        selection.Move(-10, -10, extend: false);

        Assert.Equal(CellAddress.First, selection.Active);
    }

    [Fact]
    public void SelezionareUnaRigaPrendeTuttaLaRiga()
    {
        var selection = new SheetSelection();
        selection.SelectRow(7);

        Assert.Equal(7, selection.Range.Start.Row);
        Assert.Equal(7, selection.Range.End.Row);
        Assert.Equal(CellAddress.MaxColumns, selection.Range.ColumnCount);
    }
}

public class GridFrozenAndResizeTests
{
    private static GridGeometry Standard() => new()
    {
        DefaultColumnWidth = 80,
        DefaultRowHeight = 20,
    };

    [Fact]
    public void SenzaBloccoNonCambiaNulla()
    {
        var geometry = Standard();

        Assert.False(geometry.HasFrozenPanes);
        Assert.Equal(0, geometry.FrozenWidth);
        Assert.Equal(0, geometry.FrozenHeight);
    }

    [Fact]
    public void LeRigheBloccateOccupanoSpazioMaNonScorrono()
    {
        var geometry = Standard();
        geometry.FrozenRows = 2;

        Assert.True(geometry.HasFrozenPanes);
        Assert.Equal(40, geometry.FrozenHeight);

        var visible = geometry.VisibleCells(0, 0, 800, 400);
        Assert.Equal(2, visible.FirstRow);
    }

    [Fact]
    public void LaParteMobileScorreSottoQuellaBloccata()
    {
        var geometry = Standard();
        geometry.FrozenRows = 1;

        var visible = geometry.VisibleCells(0, 200, 800, 400);

        Assert.True(visible.FirstRow > geometry.FrozenRows);
    }

    [Fact]
    public void ILatiDelleColonneSiAfferranoConUnaTolleranza()
    {
        var geometry = Standard();

        Assert.Equal(0, geometry.ColumnBorderAt(80));
        Assert.Equal(0, geometry.ColumnBorderAt(78));
        Assert.Equal(0, geometry.ColumnBorderAt(82));

        Assert.Equal(-1, geometry.ColumnBorderAt(40));
    }

    [Fact]
    public void ILatiDelleRigheSiAfferranoAllaStessaManiera()
    {
        var geometry = Standard();

        Assert.Equal(0, geometry.RowBorderAt(20));
        Assert.Equal(1, geometry.RowBorderAt(40));
        Assert.Equal(-1, geometry.RowBorderAt(10));
    }

    [Fact]
    public void RidimensionareUnaColonnaSpostaQuelleSuccessive()
    {
        var geometry = Standard();
        geometry.SetColumnWidth(0, 200);

        Assert.Equal(200, geometry.ColumnWidth(0));
        Assert.Equal(200, geometry.ColumnOffset(1));
        Assert.Equal(0, geometry.ColumnBorderAt(200));
    }

    [Fact]
    public void ConMolteRigheNascosteLaRigaSottoIlPuntatoreSiTrovaSubito()
    {
        var geometry = Standard();
        for (var row = 1; row <= 20000; row++) geometry.Layout.HideRow(row);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.Equal(0, geometry.RowAt(5));
        Assert.Equal(20001, geometry.RowAt(25));
        Assert.Equal(20002, geometry.RowAt(45));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UnaColonnaLargaNonSpostaLaRicerca()
    {
        var geometry = Standard();
        geometry.SetColumnWidth(0, 2000);

        Assert.Equal(0, geometry.ColumnAt(1500));
        Assert.Equal(1, geometry.ColumnAt(geometry.ColumnOffset(1) + 1));
    }
}
