using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class SortListsTests
{
    private static readonly ISheetSort Sorter = new SheetSorter();

    private static readonly string[] Months = ["gen", "feb", "mar", "apr"];

    private static Worksheet SheetWith(params string[] values)
    {
        var sheet = new Worksheet("Foglio1");

        for (var row = 0; row < values.Length; row++)
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text(values[row]));

        return sheet;
    }

    private static IReadOnlyList<string> ColumnOf(Worksheet sheet, int count) =>
        [.. Enumerable.Range(0, count).Select(row => sheet.GetValue(new CellAddress(row, 0)).AsText())];

    [Fact]
    public void SenzaElencoSiOrdinaInOrdineAlfabetico()
    {
        var sheet = SheetWith("mar", "gen", "feb");

        Sorter.Sort(sheet, new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)), [new SortKey(0)]);

        Assert.Equal(["feb", "gen", "mar"], ColumnOf(sheet, 3));
    }

    [Fact]
    public void ConUnElencoPersonalizzatoSiSegueQuellOrdine()
    {
        var sheet = SheetWith("mar", "gen", "feb");

        Sorter.Sort(
            sheet, new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)),
            [new SortKey(0) { CustomOrder = Months }]);

        Assert.Equal(["gen", "feb", "mar"], ColumnOf(sheet, 3));
    }

    [Fact]
    public void QuelloCheNonEnellElencoFinisceInFondo()
    {
        var sheet = SheetWith("zulu", "feb", "gen");

        Sorter.Sort(
            sheet, new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)),
            [new SortKey(0) { CustomOrder = Months }]);

        Assert.Equal(["gen", "feb", "zulu"], ColumnOf(sheet, 3));
    }

    [Fact]
    public void LOrdineDiscendenteRibaltaAncheLElenco()
    {
        var sheet = SheetWith("gen", "mar", "feb");

        Sorter.Sort(
            sheet, new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)),
            [new SortKey(0, Ascending: false) { CustomOrder = Months }]);

        Assert.Equal(["mar", "feb", "gen"], ColumnOf(sheet, 3));
    }

    [Fact]
    public void LElencoSiRiconosceDaiValori()
    {
        var lists = new SortLists([Months, ["lun", "mar", "mer"]]);

        var found = lists.ContainingAll(["feb", "gen"]);

        Assert.NotNull(found);
        Assert.Equal(Months, found);
        Assert.Null(lists.ContainingAll(["gen", "lun"]));
    }

    [Fact]
    public void LaDistinzioneFraMaiuscoleEMinuscoleSiPuoChiedere()
    {
        var sheet = SheetWith("beta", "Beta", "alfa");

        Sorter.Sort(
            sheet, new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)),
            [new SortKey(0) { CaseSensitive = true }]);

        Assert.Equal("alfa", ColumnOf(sheet, 3)[0]);
    }

    [Fact]
    public void NellOrdineDiscendenteQuelloFuoriElencoRestaInFondo()
    {
        var sheet = SheetWith("zulu", "gen", "mar");

        Sorter.Sort(
            sheet, new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)),
            [new SortKey(0, Ascending: false) { CustomOrder = Months }]);

        Assert.Equal(["mar", "gen", "zulu"], ColumnOf(sheet, 3));
    }
}
