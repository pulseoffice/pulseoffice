using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class WorkbookComparisonTests
{
    private readonly WorkbookComparer _comparer = new();
    private readonly WorkbookMerger _merger = new();

    private static CellAddress At(string text)
    {
        Assert.True(CellAddress.TryParseA1(text, out var address));
        return address;
    }

    private static Workbook With(params (string Address, double Value)[] cells)
    {
        var workbook = new Workbook();
        var sheet = workbook.AddSheet("Dati");

        foreach (var (address, value) in cells)
            sheet.SetValue(At(address), CellValue.Number(value));

        return workbook;
    }

    [Fact]
    public void DueDocumentiUgualiNonProduconoDifferenze()
    {
        Assert.True(_comparer.Compare(With(("A1", 1)), With(("A1", 1))).IsEmpty);
    }

    [Fact]
    public void UnValoreCambiatoPortaPrimaEDopo()
    {
        var difference = Assert.Single(
            _comparer.Compare(With(("A1", 1)), With(("A1", 2))).CellChanges);

        Assert.Equal(At("A1"), difference.Address);
        Assert.Equal(1d, difference.Before.AsNumber());
        Assert.Equal(2d, difference.After.AsNumber());
    }

    [Fact]
    public void UnaCellaAggiuntaDallAltroDocumentoContaComeDifferenza()
    {
        var difference = Assert.Single(
            _comparer.Compare(With(("A1", 1)), With(("A1", 1), ("B2", 5))).CellChanges);

        Assert.True(difference.Before.IsEmpty);
        Assert.Equal(5d, difference.After.AsNumber());
    }

    [Fact]
    public void UnaCellaSparitaNellAltroDocumentoContaComeDifferenza()
    {
        var difference = Assert.Single(
            _comparer.Compare(With(("A1", 1), ("B2", 5)), With(("A1", 1))).CellChanges);

        Assert.Equal(5d, difference.Before.AsNumber());
        Assert.True(difference.After.IsEmpty);
    }

    [Fact]
    public void LaStessaFormulaConValoriDiversiNonEUnaDifferenza()
    {
        var mine = With(("A1", 1));
        var theirs = With(("A1", 1));

        mine.Sheets[0].SetFormula(At("B1"), "=A1");
        theirs.Sheets[0].SetFormula(At("B1"), "=A1");

        Assert.True(_comparer.Compare(mine, theirs).IsEmpty);
    }

    [Fact]
    public void UnFoglioSoloLoroSiSegnalaCcomeAggiunto()
    {
        var theirs = With(("A1", 1));
        theirs.AddSheet("Nuovo");

        var difference = Assert.Single(_comparer.Compare(With(("A1", 1)), theirs).SheetChanges);

        Assert.Equal(SheetDifferenceKind.Added, difference.Kind);
        Assert.Equal("Nuovo", difference.SheetName);
    }

    [Fact]
    public void LUnioneAggiungeIlFoglioMancante()
    {
        var mine = With(("A1", 1));
        var theirs = With(("A1", 1));
        theirs.AddSheet("Nuovo");

        _merger.Merge(mine, _comparer.Compare(mine, theirs));

        Assert.NotNull(mine.FindSheet("Nuovo"));
    }

    [Fact]
    public void LeDifferenzeEntranoNelRegistroAncheSenzaRegistrazioneAttiva()
    {
        var mine = With(("A1", 1));

        Assert.False(mine.Changes.IsRecording);

        _merger.Merge(mine, _comparer.Compare(mine, With(("A1", 2))));

        Assert.Single(mine.Changes.Changes);
        Assert.Equal(1, mine.Changes.PendingCount);
    }
}
