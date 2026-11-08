using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class AdvancedFilterTests
{
    private static readonly ISheetFilter Filter = new SheetFilterService();

    private static Worksheet Sheet()
    {
        var sheet = new Worksheet("Foglio1");

        object[][] rows =
        [
            ["Regione", "Importo"],
            ["Nord", 100.0],
            ["Sud", 20.0],
            ["Nord", 5.0],
            ["Est", 50.0],
        ];

        for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < rows[row].Length; column++)
                sheet.SetValue(new CellAddress(row, column), rows[row][column] switch
                {
                    double number => CellValue.Number(number),
                    var text => CellValue.Text(text.ToString()!),
                });

        return sheet;
    }

    private static CellRange Data => new(new CellAddress(0, 0), new CellAddress(4, 1));

    private static void Criteria(Worksheet sheet, params (int Row, int Column, string Value)[] cells)
    {
        foreach (var (row, column, value) in cells)
            sheet.SetValue(new CellAddress(row, column), CellValue.Text(value));
    }

    [Fact]
    public void UnCriterioSuUnaColonnaNascondeIlResto()
    {
        var sheet = Sheet();
        Criteria(sheet, (0, 4, "Regione"), (1, 4, "Nord"));

        var hidden = Filter.RowsToHide(sheet, new AdvancedFilterRequest(
            Data, new CellRange(new CellAddress(0, 4), new CellAddress(1, 4))));

        Assert.Equal([2, 4], hidden.Order());
    }

    [Fact]
    public void DueCriteriSullaStessaRigaValgonoInsieme()
    {
        var sheet = Sheet();
        Criteria(sheet, (0, 4, "Regione"), (0, 5, "Importo"), (1, 4, "Nord"), (1, 5, ">50"));

        var hidden = Filter.RowsToHide(sheet, new AdvancedFilterRequest(
            Data, new CellRange(new CellAddress(0, 4), new CellAddress(1, 5))));

        Assert.Equal([2, 3, 4], hidden.Order());
    }

    [Fact]
    public void DueRigheDiCriteriValgonoInAlternativa()
    {
        var sheet = Sheet();
        Criteria(sheet, (0, 4, "Regione"), (1, 4, "Nord"), (2, 4, "Est"));

        var hidden = Filter.RowsToHide(sheet, new AdvancedFilterRequest(
            Data, new CellRange(new CellAddress(0, 4), new CellAddress(2, 4))));

        Assert.Equal([2], hidden.Order());
    }

    [Fact]
    public void SenzaCriteriNonSiNascondeNulla()
    {
        var sheet = Sheet();
        Criteria(sheet, (0, 4, "Regione"));

        var hidden = Filter.RowsToHide(sheet, new AdvancedFilterRequest(
            Data, new CellRange(new CellAddress(0, 4), new CellAddress(0, 4))));

        Assert.Empty(hidden);
    }

    [Fact]
    public void ISoliValoriUniciTolgonoLeRipetizioni()
    {
        var sheet = Sheet();
        sheet.SetValue(new CellAddress(3, 1), CellValue.Number(100));
        Criteria(sheet, (0, 4, "Regione"));

        var hidden = Filter.RowsToHide(sheet, new AdvancedFilterRequest(
            Data, new CellRange(new CellAddress(0, 4), new CellAddress(0, 4))) { UniqueOnly = true });

        Assert.Equal([3], hidden.Order());
    }

    [Fact]
    public void UnaColonnaDeiCriteriCheNonEsisteVieneIgnorata()
    {
        var sheet = Sheet();
        Criteria(sheet, (0, 4, "Inesistente"), (1, 4, "Nord"));

        var hidden = Filter.RowsToHide(sheet, new AdvancedFilterRequest(
            Data, new CellRange(new CellAddress(0, 4), new CellAddress(1, 4))));

        Assert.Empty(hidden);
    }

    [Fact]
    public void UnCriterioNumericoDecimaleValeAncheInItaliano()
    {
        var filter = new SheetFilterService(new PulseOffice.Sheets.Engine.Evaluation.Functions.NumericText(
            new PulseOffice.Sheets.Formatting.FixedSpreadsheetLocale(new System.Globalization.CultureInfo("it-IT"))));
        var sheet = Sheet();
        sheet.SetValue(new CellAddress(2, 1), CellValue.Number(1.5));
        sheet.SetValue(new CellAddress(10, 1), CellValue.Text("Importo"));
        sheet.SetValue(new CellAddress(11, 1), CellValue.Number(1.5));

        var hidden = filter.RowsToHide(sheet, new AdvancedFilterRequest(Data, new CellRange(new CellAddress(10, 1), new CellAddress(11, 1))));

        Assert.Equal([1, 3, 4], hidden.Order());
    }
}
