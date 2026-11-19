using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public sealed class Round264PivotTests
{
    private sealed class Table(object[][] rows) : IPivotSource
    {
        public IReadOnlyList<string> Headers { get; } = ["Region", "Year", "Amount"];

        public int RowCount => rows.Length;

        public CellValue ValueAt(int row, int column) => rows[row][column] switch
        {
            int number => CellValue.Number(number),
            bool truth => CellValue.Boolean(truth),
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
        ["South", 2024, 40],
    ]);

    private static PivotField Region => new() { SourceName = "Region", SourceColumnIndex = 0, Area = PivotArea.Row };

    private static PivotField Year => new() { SourceName = "Year", SourceColumnIndex = 1, Area = PivotArea.Row, AreaOrder = 1 };

    private static PivotField Amount(ValueDisplayMode mode, string? baseField = null) =>
        new() { SourceName = "Amount", SourceColumnIndex = 2, Area = PivotArea.Value, DisplayMode = mode, BaseField = baseField };

    private static PivotDefinition Definition(params PivotField[] fields) => new()
    {
        Id = "p",
        Name = "P",
        SourceSheet = "Data",
        SourceRange = new CellRange(CellAddress.First, new CellAddress(4, 2)),
        DestinationSheet = "Pivot",
        Destination = CellAddress.First,
        Fields = fields,
    };

    private static double? ValueOf(PivotTable table, PivotNodeKind kind, params string[] path)
    {
        var row = table.Rows.ToList().FindIndex(node => node.Kind == kind && node.Path.SequenceEqual(path));
        var value = table.Value(row, 0);
        return value.IsNumeric ? value.AsNumber() : null;
    }

    [Fact]
    public void Percent_of_parent_divides_by_the_parent_row_total()
    {
        var table = Engine.Build(Definition(Region, Year, Amount(ValueDisplayMode.PercentOfParent)), Sales);

        Assert.Equal(0.25, ValueOf(table, PivotNodeKind.Item, "North", "2023"));
        Assert.Equal(40.0 / 60, ValueOf(table, PivotNodeKind.Item, "South", "2024"));
        Assert.Equal(0.4, ValueOf(table, PivotNodeKind.Subtotal, "North"));
    }

    [Fact]
    public void Difference_follows_the_chosen_base_field()
    {
        var table = Engine.Build(Definition(Region, Year, Amount(ValueDisplayMode.DifferenceFrom, "Year")), Sales);

        Assert.Null(ValueOf(table, PivotNodeKind.Item, "North", "2023"));
        Assert.Equal(20, ValueOf(table, PivotNodeKind.Item, "North", "2024"));
        Assert.Equal(20, ValueOf(table, PivotNodeKind.Item, "South", "2024"));
    }

    [Fact]
    public void Running_total_restarts_inside_the_items_above_the_base_field()
    {
        var table = Engine.Build(Definition(Region, Year, Amount(ValueDisplayMode.RunningTotal, "Year")), Sales);

        Assert.Equal(40, ValueOf(table, PivotNodeKind.Item, "North", "2024"));
        Assert.Equal(20, ValueOf(table, PivotNodeKind.Item, "South", "2023"));
    }

    [Fact]
    public void Boolean_items_are_keyed_independently_of_the_language_and_render_as_booleans()
    {
        Assert.Equal("TRUE", PivotValues.KeyOf(CellValue.Boolean(true)));

        var flags = new Table([[true, 2023, 1], [false, 2023, 2]]);
        var definition = Definition(Region, Amount(ValueDisplayMode.Normal));
        var sheet = Workbook.CreateDefault().Sheets[0];
        new PivotRenderer().Render(Engine.Build(definition, flags), definition, sheet, new StyleCatalog(), Labels);

        var labels = Enumerable.Range(1, 2).Select(row => sheet.GetValue(new CellAddress(row, 0))).ToList();
        Assert.All(labels, label => Assert.Equal(ValueKind.Boolean, label.Kind));
    }

    [Fact]
    public void A_value_field_number_format_styles_its_cells()
    {
        var definition = Definition(Region, Amount(ValueDisplayMode.Normal) with { NumberFormat = "0.0 \"kg\"" });
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        new PivotRenderer().Render(Engine.Build(definition, Sales), definition, sheet, workbook.Styles, Labels, workbook.NumberFormats);

        var style = workbook.Styles.Get(sheet[new CellAddress(1, 1)].StyleId);
        Assert.Equal("0.0 \"kg\"", workbook.NumberFormats.CodeOf(style.NumberFormatId));
    }

    [Fact]
    public void Date_group_labels_follow_the_labels_language_while_keys_stay_invariant()
    {
        var dates = new Table([[45366, 2024, 1]]);
        var quarter = new PivotField { SourceName = "Region", SourceColumnIndex = 0, Area = PivotArea.Row, DateGrouping = DateGrouping.Quarters };
        var month = new PivotField { SourceName = "Year", SourceColumnIndex = 1, Area = PivotArea.Column };
        var definition = Definition(quarter, Amount(ValueDisplayMode.Normal));
        var italian = Labels with { Quarter = "T{1} {0}", Culture = System.Globalization.CultureInfo.GetCultureInfo("it") };
        var sheet = Workbook.CreateDefault().Sheets[0];
        var table = Engine.Build(definition, dates);
        new PivotRenderer().Render(table, definition, sheet, new StyleCatalog(), italian);

        Assert.Equal("2024-T1", table.Rows[0].Label);
        Assert.Equal("T1 2024", sheet.GetValue(new CellAddress(1, 0)).AsText());
        Assert.Equal("mar 2024", new PivotDateLabels().Label("2024-03", month with { DateGrouping = DateGrouping.Months }, italian));
    }
}
