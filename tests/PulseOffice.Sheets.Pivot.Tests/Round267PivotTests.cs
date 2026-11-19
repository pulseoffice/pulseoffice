using System.Globalization;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Styling;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public sealed class Round267PivotTests
{
    private sealed class Table(CellValue[][] rows) : IPivotSource
    {
        public IReadOnlyList<string> Headers { get; } = ["Key", "Year", "Amount"];

        public int RowCount => rows.Length;

        public CellValue ValueAt(int row, int column) => rows[row][column];
    }

    private static readonly IPivotEngine Engine = new PivotEngine(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser()));

    private static readonly PivotLabels Italian = new("Totale", "Totale {0}") { Culture = CultureInfo.GetCultureInfo("it") };

    private static CellValue[] Row(CellValue key, double year, double amount) => [key, CellValue.Number(year), CellValue.Number(amount)];

    private static PivotField Key => new() { SourceName = "Key", SourceColumnIndex = 0, Area = PivotArea.Row };

    private static PivotField Year(PivotArea area, int order = 0) => new() { SourceName = "Year", SourceColumnIndex = 1, Area = area, AreaOrder = order };

    private static PivotField Amount(ValueDisplayMode mode = ValueDisplayMode.Normal) =>
        new() { SourceName = "Amount", SourceColumnIndex = 2, Area = PivotArea.Value, DisplayMode = mode, BaseField = "Year" };

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

    private static double? ValueOf(PivotTable table, params string[] path)
    {
        var row = table.Rows.ToList().FindIndex(node => node.Kind == PivotNodeKind.Item && node.Path.SequenceEqual(path));
        var value = table.Value(row, 0);
        return value.IsNumeric ? value.AsNumber() : null;
    }

    [Fact]
    public void A_text_TRUE_is_its_own_item_and_renders_as_text()
    {
        var source = new Table([Row(CellValue.Boolean(true), 1, 1), Row(CellValue.Text("TRUE"), 1, 2), Row(CellValue.Text("5"), 1, 4), Row(CellValue.Number(5), 1, 8)]);
        var definition = Definition(Key, Amount());
        var table = Engine.Build(definition, source);

        Assert.Equal(4, table.Rows.Count(node => node.Kind == PivotNodeKind.Item));
        var sheet = Workbook.CreateDefault().Sheets[0];
        new PivotRenderer().Render(table, definition, sheet, new StyleCatalog(), Italian);
        var labels = Enumerable.Range(1, 4).Select(row => sheet.GetValue(new CellAddress(row, 0))).ToList();
        Assert.Contains(labels, label => label.Kind == ValueKind.Text && label.AsText() == "TRUE");
        Assert.Contains(labels, label => label.Kind == ValueKind.Boolean);
        Assert.Contains(labels, label => label.Kind == ValueKind.Text && label.AsText() == "5");
        Assert.Contains(labels, label => label.Kind == ValueKind.Number);
    }

    [Fact]
    public void Subtotal_labels_of_boolean_items_use_the_language()
    {
        var source = new Table([Row(CellValue.Boolean(true), 2023, 1), Row(CellValue.Boolean(true), 2024, 2)]);
        var definition = Definition(Key, Year(PivotArea.Row, 1), Amount());
        var sheet = Workbook.CreateDefault().Sheets[0];
        new PivotRenderer().Render(Engine.Build(definition, source), definition, sheet, new StyleCatalog(), Italian);

        var texts = Enumerable.Range(0, 8).SelectMany(row => Enumerable.Range(0, 3).Select(column => sheet.GetValue(new CellAddress(row, column))))
            .Where(value => value.Kind == ValueKind.Text).Select(value => value.AsText()).ToList();
        Assert.Contains("Totale VERO", texts);
        Assert.DoesNotContain("Totale TRUE", texts);
    }

    [Fact]
    public void Difference_from_a_named_or_next_base_item()
    {
        var source = new Table([Row(CellValue.Text("A"), 2023, 10), Row(CellValue.Text("A"), 2024, 30), Row(CellValue.Text("A"), 2025, 60)]);
        var named = Engine.Build(Definition(Key, Year(PivotArea.Row, 1), Amount(ValueDisplayMode.DifferenceFrom) with { BaseItem = PivotBaseItem.Named("2023") }), source);
        var next = Engine.Build(Definition(Key, Year(PivotArea.Row, 1), Amount(ValueDisplayMode.DifferenceFrom) with { BaseItem = PivotBaseItem.Next }), source);

        Assert.Null(ValueOf(named, "A", "2023"));
        Assert.Equal(50, ValueOf(named, "A", "2025"));
        Assert.Equal(-20, ValueOf(next, "A", "2023"));
        Assert.Null(ValueOf(next, "A", "2025"));
    }

    [Fact]
    public void Percent_of_parent_column_divides_by_the_parent_column_total()
    {
        var source = new Table([Row(CellValue.Text("A"), 2023, 10), Row(CellValue.Text("A"), 2024, 30)]);
        var table = Engine.Build(Definition(Key, Year(PivotArea.Column), Amount(ValueDisplayMode.PercentOfParentColumn)), source);

        var column = table.Columns.ToList().FindIndex(entry => entry.Node.Kind == PivotNodeKind.Item && entry.Node.Path.SequenceEqual(["2024"]));
        Assert.Equal(0.75, table.Value(0, column).AsNumber());
    }

    [Fact]
    public void GetPivotData_accepts_localized_date_labels_and_text_items_named_like_booleans()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Name = "Data";
        string[] headers = ["Key", "Day", "Amount"];
        for (var c = 0; c < 3; c++) workbook.Sheets[0].SetValue(new CellAddress(0, c), CellValue.Text(headers[c]));
        workbook.Sheets[0].SetValue(new CellAddress(1, 0), CellValue.Text("TRUE"));
        workbook.Sheets[0].SetValue(new CellAddress(1, 1), CellValue.Number(45366));
        workbook.Sheets[0].SetValue(new CellAddress(1, 2), CellValue.Number(7));
        workbook.AddSheet("Pivot");
        var quarter = new PivotField { SourceName = "Day", SourceColumnIndex = 1, Area = PivotArea.Column, DateGrouping = DateGrouping.Quarters };
        workbook.Pivots.Add(Definition(Key, quarter, Amount()) with { SourceRange = new CellRange(CellAddress.First, new CellAddress(1, 2)) });
        var lookup = new PivotDataLookup(workbook, Engine, Italian with { Quarter = "T{1} {0}" });

        Assert.Equal(7, lookup.Lookup(1, new CellAddress(2, 2), "Amount", [("Day", "T1 2024"), ("Key", "TRUE")])?.AsNumber());
        Assert.Equal(7, lookup.Lookup(1, new CellAddress(2, 2), "Amount", [("Day", "2024-T1")])?.AsNumber());
    }
}
