using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class SelectionWorkAreaTests
{
    private readonly SelectionWorkArea _area = new();

    private static CellRange Range(int r1, int c1, int r2, int c2) => new(new CellAddress(r1, c1), new CellAddress(r2, c2));

    [Fact]
    public void UnIntervalloNormaleResta() =>
        Assert.Equal(Range(2, 1, 9, 3), _area.Bound(Range(2, 1, 9, 3), Range(0, 0, 4, 4)));

    [Fact]
    public void UnaColonnaInteraSiLimitaAlleRigheUsate() =>
        Assert.Equal(Range(3, 2, 40, 2), _area.Bound(Range(0, 2, CellAddress.MaxRows - 1, 2), Range(3, 0, 40, 10)));

    [Fact]
    public void UnaRigaInteraSiLimitaAlleColonneUsate() =>
        Assert.Equal(Range(5, 1, 5, 7), _area.Bound(Range(5, 0, 5, CellAddress.MaxColumns - 1), Range(0, 1, 9, 7)));

    [Fact]
    public void SuUnFoglioVuotoNonCeNullaDaFare() =>
        Assert.Null(_area.Bound(Range(0, 2, CellAddress.MaxRows - 1, 2), null));

    [Fact]
    public void UnaColonnaInteraImpostaIlPredefinitoESoloLeCelleUsate()
    {
        var work = _area.Plan(Range(0, 2, CellAddress.MaxRows - 1, 2), Range(0, 0, 3, 3));

        Assert.Equal([new IndexSpan(2, 2)], work.Columns);
        Assert.Empty(work.Rows);
        Assert.Contains(new CellAddress(3, 2), work.Cells);
        Assert.Equal(4, work.CellCount);
    }

    [Fact]
    public void UnaRigaInteraSuUnFoglioVuotoNonToccaCelle()
    {
        var work = _area.Plan(Range(4, 0, 4, CellAddress.MaxColumns - 1), null);

        Assert.Equal([new IndexSpan(4, 4)], work.Rows);
        Assert.Empty(work.Cells);
    }

    [Fact]
    public void TuttoIlFoglioImpostaOgniColonnaEOgniRiga()
    {
        var work = _area.Plan(Range(0, 0, CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1), null);

        Assert.Equal([new IndexSpan(0, CellAddress.MaxColumns - 1)], work.Columns);
        Assert.Equal([new IndexSpan(0, CellAddress.MaxRows - 1)], work.Rows);
    }

    [Fact]
    public void UnIntervalloNormaleNonImpostaPredefiniti() =>
        Assert.False(_area.Plan(Range(1, 1, 2, 2), null).SetsDefaults);

    [Fact]
    public void TuttoIlFoglioDiventaLAreaUsata() =>
        Assert.Equal(Range(1, 1, 3, 3), _area.Bound(Range(0, 0, CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1), Range(1, 1, 3, 3)));

    [Fact]
    public void AlmostAllWholeRowsWithStyledColumnsPlanOnlyTheUsedArea()
    {
        var work = _area.Plan(Range(1, 0, CellAddress.MaxRows - 1, CellAddress.MaxColumns - 1), Range(0, 0, 9, 9));

        Assert.Equal([new IndexSpan(1, CellAddress.MaxRows - 1)], work.Rows);
        Assert.Equal([Range(1, 0, 9, 9)], work.CellAreas);
        Assert.Equal(90, work.CellCount);
    }
}
