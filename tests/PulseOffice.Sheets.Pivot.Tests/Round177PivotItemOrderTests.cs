using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public class Round177PivotItemOrderTests
{
    private static readonly IPivotEngine Engine = new PivotEngine(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser()));

    private static string[] ItemsOf(bool descending, params string[] keys)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Key"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Amount"));
        for (var row = 0; row < keys.Length; row++)
        {
            sheet.SetValue(new CellAddress(row + 1, 0), double.TryParse(keys[row], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) ? CellValue.Number(number) : CellValue.Text(keys[row]));
            sheet.SetValue(new CellAddress(row + 1, 1), CellValue.Number(1));
        }
        var definition = new PivotDefinition
        {
            Id = "p1", Name = "P", SourceSheet = sheet.Name,
            SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(keys.Length, 1)),
            DestinationSheet = "Pivot", Destination = CellAddress.First,
            Fields =
            [
                new PivotField { SourceName = "Key", SourceColumnIndex = 0, Area = PivotArea.Row, SortDescending = descending },
                new PivotField { SourceName = "Amount", SourceColumnIndex = 1, Area = PivotArea.Value },
            ],
        };
        var table = Engine.Build(definition, new RangePivotSource(sheet, definition.SourceRange));
        return [.. table.Rows.Where(node => node.Kind == PivotNodeKind.Item).Select(node => node.Path[^1])];
    }

    [Fact]
    public void Numbers_come_before_text_whatever_the_input_order()
    {
        var first = ItemsOf(false, "B", "10", "a1", "2");
        var second = ItemsOf(false, "a1", "2", "B", "10");
        Assert.Equal(first, second);
        Assert.Equal(["2", "10"], first.Take(2));
    }

    [Fact]
    public void Descending_is_the_exact_reverse_of_ascending()
    {
        var ascending = ItemsOf(false, "10", "x", "9", "Y", "1");
        var descending = ItemsOf(true, "10", "x", "9", "Y", "1");
        Assert.Equal(ascending.Reverse(), descending);
    }
}
