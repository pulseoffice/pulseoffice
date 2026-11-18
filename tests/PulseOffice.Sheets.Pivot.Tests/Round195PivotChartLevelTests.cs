using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public class Round195PivotChartLevelTests
{
    private static readonly PivotLabels Labels = new("Total", "Total {0}");

    private static readonly PivotCoordinator Coordinator =
        new(new PivotEngine(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser())), new PivotRenderer());

    private static readonly string[][] Rows =
    [
        ["Region", "City", "Year", "Quarter"],
        ["A", "x", "Y1", "Q1"],
        ["A", "y", "Y1", "Q1"],
        ["B", "x", "Y1", "Q1"],
    ];

    private static Workbook Data()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < Rows.Length; row++)
        {
            for (var column = 0; column < 4; column++) sheet.SetValue(new CellAddress(row, column), CellValue.Text(Rows[row][column]));
            sheet.SetValue(new CellAddress(row, 4), row == 0 ? CellValue.Text("Amount") : CellValue.Number(row));
        }
        return workbook;
    }

    private static PivotDefinition Definition(Workbook workbook) => new()
    {
        Id = "p1", Name = "Sales", SourceSheet = workbook.Sheets[0].Name,
        SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(3, 4)),
        DestinationSheet = "Pivot", Destination = CellAddress.First,
        Fields =
        [
            new PivotField { SourceName = "Region", SourceColumnIndex = 0, Area = PivotArea.Row },
            new PivotField { SourceName = "City", SourceColumnIndex = 1, Area = PivotArea.Row },
            new PivotField { SourceName = "Year", SourceColumnIndex = 2, Area = PivotArea.Column },
            new PivotField { SourceName = "Quarter", SourceColumnIndex = 3, Area = PivotArea.Column },
            new PivotField { SourceName = "Amount", SourceColumnIndex = 4, Area = PivotArea.Value },
        ],
    };

    private static Workbook WithChart()
    {
        var workbook = Data();
        Assert.True(Coordinator.Apply(workbook, Definition(workbook), Labels).Succeeded);
        workbook.FindSheet("Pivot")!.Objects.Add(new PlacedObject
        {
            Id = "c",
            Anchor = new CellAddress(0, 8),
            Content = new ChartDefinition
            {
                Id = "c", Type = ChartType.Column, SourceSheet = "Pivot", SourceRange = "A1:B2",
                PivotSource = new ChartPivotSource("Sales"),
            },
        });
        return workbook;
    }

    private static ChartDefinition ChartOf(Workbook workbook) =>
        Assert.IsType<ChartDefinition>(Assert.Single(workbook.FindSheet("Pivot")!.Objects).Content);

    [Fact]
    public void Several_row_fields_give_the_chart_outer_category_levels()
    {
        var workbook = WithChart();

        Assert.True(Coordinator.Apply(workbook, Definition(workbook), Labels).Succeeded);

        var data = new ChartDataExtractor().Extract(workbook, ChartOf(workbook));
        Assert.Equal(["x", "y", "x"], data.Categories);
        Assert.Equal(["A", "", "B"], Assert.Single(data.CategoryLevels!));
    }

    [Fact]
    public void A_detached_pivot_chart_keeps_its_levels_in_the_inline_data()
    {
        var workbook = WithChart();
        Assert.True(Coordinator.Apply(workbook, Definition(workbook), Labels).Succeeded);

        Assert.Equal(1, new PivotChartLinks().Detach(workbook, Definition(workbook)));

        var chart = ChartOf(workbook);
        Assert.Null(chart.CategoryLevels);
        Assert.Equal(["A", "", "B"], Assert.Single(chart.InlineData!.CategoryLevels!));
    }
}
