using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class MergedRegionsTests
{
    private static CellRange R(string a1)
    {
        Assert.True(CellRange.TryParseA1(a1, out var range));
        return range;
    }

    [Fact]
    public void UnaRegioneDiUnaCellaSolaNonEUnUnione()
    {
        var merges = new MergedRegions();

        Assert.False(merges.Add(R("A1")));
        Assert.Equal(0, merges.Count);
    }

    [Fact]
    public void LaCellaCopertaSaDiEsserlo()
    {
        var merges = new MergedRegions();
        merges.Add(R("B2:D3"));

        Assert.True(merges.IsAnchor(new CellAddress(1, 1)));
        Assert.True(merges.IsCovered(new CellAddress(1, 2)));
        Assert.False(merges.IsCovered(new CellAddress(1, 1)));
        Assert.Null(merges.At(new CellAddress(5, 5)));
    }

    [Fact]
    public void UnireSopraUnUnioneEsistenteLaSostituisce()
    {
        var merges = new MergedRegions();
        merges.Add(R("B2:C3"));
        merges.Add(R("B2:E5"));

        Assert.Equal(1, merges.Count);
        Assert.Equal(R("B2:E5"), merges.All[0]);
    }

    [Fact]
    public void LaSelezioneSiAllargaAlleUnioniCheTocca()
    {
        var merges = new MergedRegions();
        merges.Add(R("C3:E4"));

        Assert.Equal(R("B2:E4"), merges.Expand(R("B2:C3")));
    }

    [Fact]
    public void LAllargamentoSiFermaQuandoNonTrovaPiuNulla()
    {
        var merges = new MergedRegions();
        merges.Add(R("A1:B2"));
        merges.Add(R("B3:C4"));

        Assert.Equal(R("A1:C4"), merges.Expand(R("A1:A3")));
    }

    [Fact]
    public void SeparareRestituisceLeUnioniTolte()
    {
        var merges = new MergedRegions();
        merges.Add(R("A1:B2"));
        merges.Add(R("D1:E2"));

        var tolte = merges.RemoveOverlapping(R("A1:C5"));

        Assert.Single(tolte);
        Assert.Equal(1, merges.Count);
    }
}
