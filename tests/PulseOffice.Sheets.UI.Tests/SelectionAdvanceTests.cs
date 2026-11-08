using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.UI;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public class SelectionAdvanceTests
{
    private static CellAddress A(string a1)
    {
        Assert.True(CellAddress.TryParseA1(a1, out var address));
        return address;
    }

    [Fact]
    public void InvioScendeDentroLaSelezioneERicominciaDallaColonnaDopo()
    {
        var selection = new SheetSelection();
        selection.MoveTo(A("B2"));
        selection.ExtendTo(A("C3"));
        var visited = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            selection.Advance(byRow: true, backwards: false);
            visited.Add(selection.Active.ToA1());
        }

        Assert.Equal(["B3", "C2", "C3", "B2", "B3"], visited);
        Assert.Equal("B2:C3", selection.Range.ToString());
    }

    [Fact]
    public void TabScorreLaRigaEMaiuscoloTornaIndietro()
    {
        var selection = new SheetSelection();
        selection.MoveTo(A("B2"));
        selection.ExtendTo(A("C3"));
        selection.Advance(byRow: false, backwards: false);
        Assert.Equal("C2", selection.Active.ToA1());
        selection.Advance(byRow: false, backwards: false);
        Assert.Equal("B3", selection.Active.ToA1());
        selection.Advance(byRow: false, backwards: true);
        Assert.Equal("C2", selection.Active.ToA1());
        Assert.Equal("B2:C3", selection.Range.ToString());
    }

    [Fact]
    public void SelezionandoUnaRigaLaCellaAttivaEInizioRiga()
    {
        var selection = new SheetSelection();
        selection.SelectRow(4);
        Assert.Equal("A5", selection.Active.ToA1());
    }

    [Fact]
    public void EstendendoLaCellaAttivaRestaQuellaDiPartenzaESiMuoveLAngolo()
    {
        var selection = new SheetSelection();
        selection.MoveTo(A("B2"));
        selection.Move(1, 0, extend: true);
        selection.Move(0, 2, extend: true);

        Assert.Equal("B2", selection.Active.ToA1());
        Assert.Equal("D3", selection.Corner.ToA1());
        Assert.Equal("B2:D3", selection.Range.ToString());

        selection.Move(-2, 0, extend: true);
        Assert.Equal("B1:D2", selection.Range.ToString());
        Assert.Equal("B2", selection.Active.ToA1());
    }

    [Fact]
    public void MuovendoSenzaEstendereSiRipartedallaCellaAttiva()
    {
        var selection = new SheetSelection();
        selection.MoveTo(A("B2"));
        selection.ExtendTo(A("D4"));
        selection.Move(0, 1, extend: false);
        Assert.Equal("C2", selection.Active.ToA1());
        Assert.True(selection.IsSingleCell);
    }

    [Fact]
    public void InvioDopoUnaRigaScrittaConTabTornaAllaColonnaDiPartenza()
    {
        var selection = new SheetSelection();
        selection.MoveTo(A("B3"));
        selection.Advance(byRow: false, backwards: false);
        selection.Advance(byRow: false, backwards: false);
        Assert.Equal("D3", selection.Active.ToA1());
        selection.Advance(byRow: true, backwards: false);
        Assert.Equal("B4", selection.Active.ToA1());
        selection.Advance(byRow: true, backwards: false);
        Assert.Equal("B5", selection.Active.ToA1());
    }

    [Fact]
    public void UnoSpostamentoDiversoDimenticaLaColonnaDiPartenza()
    {
        var selection = new SheetSelection();
        selection.MoveTo(A("B3"));
        selection.Advance(byRow: false, backwards: false);
        selection.MoveTo(A("F3"));
        selection.Advance(byRow: true, backwards: false);
        Assert.Equal("F4", selection.Active.ToA1());
    }
}
