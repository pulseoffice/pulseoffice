using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Pivot;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public sealed class Round261PivotTests
{
    private sealed class Table(object[][] rows) : IPivotSource
    {
        public IReadOnlyList<string> Headers { get; } = ["Region", "Year", "Amount"];

        public int RowCount => rows.Length;

        public CellValue ValueAt(int row, int column) => rows[row][column] switch
        {
            int number => CellValue.Number(number),
            "" => CellValue.Empty,
            var text => CellValue.Text(text.ToString()!),
        };
    }

    private static readonly IPivotEngine Engine = new PivotEngine(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser()));
    private static readonly PivotLabels Labels = new("Grand total", "{0} total", "(blank)");

    private static readonly Table Sales = new(
    [
        ["North", 2023, 10],
        ["North", 2024, 30],
        ["South", 2023, 20],
        ["South", 2024, 10],
        ["West", 2024, 5],
        ["", 2024, 1],
    ]);

    private static PivotField Region(PivotArea area = PivotArea.Row) => new() { SourceName = "Region", SourceColumnIndex = 0, Area = area };

    private static PivotField Year(PivotArea area = PivotArea.Row, int order = 0) => new() { SourceName = "Year", SourceColumnIndex = 1, Area = area, AreaOrder = order };

    private static PivotField Amount(ValueDisplayMode mode = ValueDisplayMode.Normal) =>
        new() { SourceName = "Amount", SourceColumnIndex = 2, Area = PivotArea.Value, DisplayMode = mode };

    private static PivotDefinition Definition(params PivotField[] fields) => new()
    {
        Id = "p",
        Name = "P",
        SourceSheet = "Data",
        SourceRange = new CellRange(CellAddress.First, new CellAddress(6, 2)),
        DestinationSheet = "Pivot",
        Destination = CellAddress.First,
        Fields = fields,
    };

    private static double? ValueOf(PivotTable table, params string[] path)
    {
        var row = table.Rows.ToList().FindIndex(node => node.Kind == PivotNodeKind.Item && node.Path.SequenceEqual(path));
        var value = table.Value(row, 0);
        return value.IsNumeric ? value.AsNumber() : null;
    }

    [Fact]
    public void Difference_from_previous_compares_with_the_previous_outer_item()
    {
        var table = Engine.Build(Definition(Year(), Amount(ValueDisplayMode.DifferenceFrom)), Sales);

        Assert.Null(ValueOf(table, "2023"));
        Assert.Equal(16, ValueOf(table, "2024"));
    }

    [Fact]
    public void Percent_difference_from_previous_divides_by_the_previous_item()
    {
        var table = Engine.Build(Definition(Year(), Amount(ValueDisplayMode.PercentDifferenceFrom)), Sales);

        Assert.Equal(16.0 / 30, ValueOf(table, "2024")!.Value, 9);
    }

    [Fact]
    public void Index_weighs_a_cell_against_its_row_and_column_totals()
    {
        var table = Engine.Build(Definition(Region(), Year(PivotArea.Column), Amount(ValueDisplayMode.Index)), Sales);

        var north = table.Rows.ToList().FindIndex(node => node.Path.SequenceEqual(["North"]));
        var column2023 = table.Columns.ToList().FindIndex(column => column.Node.Path.SequenceEqual(["2023"]));

        Assert.Equal(10.0 * 76 / (40 * 30), table.Value(north, column2023).AsNumber(), 9);
    }

    [Fact]
    public void Sorting_by_value_orders_items_by_their_totals()
    {
        var region = Region() with { SortByValue = 0, SortDescending = true };
        var table = Engine.Build(Definition(region, Amount()) with { ShowGrandTotalRows = false }, Sales);

        Assert.Equal(["North", "South", "West", ""], table.Rows.Select(node => node.Label));
    }

    [Fact]
    public void A_top_filter_keeps_the_largest_items_and_totals_only_them()
    {
        var region = Region() with { TopFilter = new PivotTopFilter(2, 0) };
        var table = Engine.Build(Definition(region, Amount()), Sales);

        Assert.Equal(["North", "South"], table.Rows.Where(node => node.Kind == PivotNodeKind.Item).Select(node => node.Label));
        Assert.Equal(70, table.Value(table.Rows.Count - 1, 0).AsNumber());
    }

    [Fact]
    public void Numeric_items_are_written_as_numbers_and_blank_items_with_the_blank_label()
    {
        var definition = Definition(Region(), Year(PivotArea.Column), Amount());
        var sheet = new Worksheet("Pivot");
        new PivotRenderer().Render(Engine.Build(definition, Sales), definition, sheet, new StyleCatalog(), Labels);

        Assert.Equal(ValueKind.Number, sheet.GetValue(new CellAddress(0, 1)).Kind);
        Assert.Equal(2023, sheet.GetValue(new CellAddress(0, 1)).AsNumber());
        Assert.Equal("(blank)", sheet.GetValue(new CellAddress(1, 0)).AsText());
    }

    [Fact]
    public void Outline_puts_each_row_field_in_its_column_and_repeat_labels_fill_the_outer_one()
    {
        var definition = Definition(Region(), Year(PivotArea.Row, 1), Amount()) with
        {
            Layout = PivotLayout.Outline,
            RepeatItemLabels = true,
            ShowGrandTotalRows = false,
        };
        var table = Engine.Build(definition, Sales);
        var sheet = new Worksheet("Pivot");
        new PivotRenderer().Render(table, definition, sheet, new StyleCatalog(), Labels);

        var leaf = table.Rows.ToList().FindIndex(node => node.Path.SequenceEqual(["North", "2023"]));
        Assert.Equal(2023, sheet.GetValue(new CellAddress(1 + leaf, 1)).AsNumber());
        Assert.Equal("North", sheet.GetValue(new CellAddress(1 + leaf, 0)).AsText());
    }

    [Fact]
    public void Percent_display_modes_are_formatted_as_percentages()
    {
        var definition = Definition(Region(), Amount(ValueDisplayMode.PercentOfTotal));
        var sheet = new Worksheet("Pivot");
        var styles = new StyleCatalog();
        new PivotRenderer().Render(Engine.Build(definition, Sales), definition, sheet, styles, Labels);

        Assert.Equal(10, styles.Get(sheet[new CellAddress(1, 1)].StyleId).NumberFormatId);
    }
}
