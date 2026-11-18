using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Pivot.Tests;

public class Round182PivotChartTests
{
    private static readonly PivotLabels Labels = new("Total", "Total {0}");

    private static readonly PivotCoordinator Coordinator =
        new(new PivotEngine(new PivotAggregator(), new PivotGrouping(), new PivotFormulas(new PulseOffice.Sheets.Engine.Parsing.FormulaParser())), new PivotRenderer());

    private static readonly ChartSourceReferences References = new();

    private static Workbook Data(int regions)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Text("Region"));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Text("Amount"));
        for (var row = 1; row <= regions; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text(((char)('A' + row - 1)).ToString()));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Number(row));
        }
        return workbook;
    }

    private static PivotDefinition Definition(Workbook workbook, int regions) => new()
    {
        Id = "p1", Name = "Sales", SourceSheet = workbook.Sheets[0].Name,
        SourceRange = new CellRange(new CellAddress(0, 0), new CellAddress(regions, 1)),
        DestinationSheet = "Pivot", Destination = CellAddress.First,
        Fields =
        [
            new PivotField { SourceName = "Region", SourceColumnIndex = 0, Area = PivotArea.Row },
            new PivotField { SourceName = "Amount", SourceColumnIndex = 1, Area = PivotArea.Value },
        ],
    };

    private static Workbook WithChart()
    {
        var workbook = Data(3);
        Assert.True(Coordinator.Apply(workbook, Definition(workbook, 2), Labels).Succeeded);
        workbook.FindSheet("Pivot")!.Objects.Add(new PlacedObject
        {
            Id = "c",
            Anchor = new CellAddress(0, 4),
            Content = new ChartDefinition
            {
                Id = "c", Type = ChartType.Column, SourceSheet = "Pivot", SourceRange = "A1:B4",
                PivotSource = new ChartPivotSource("Sales"),
            },
        });
        return workbook;
    }

    private static ChartDefinition ChartOf(Workbook workbook) =>
        Assert.IsType<ChartDefinition>(Assert.Single(workbook.FindSheet("Pivot")!.Objects).Content);

    [Fact]
    public void A_refresh_that_grows_the_pivot_grows_its_chart_without_the_grand_total()
    {
        var workbook = WithChart();

        Assert.True(Coordinator.Apply(workbook, Definition(workbook, 3), Labels).Succeeded);

        var source = Assert.Single(ChartOf(workbook).SeriesSources!);
        Assert.Equal(References.Write("Pivot", new CellRange(new CellAddress(1, 1), new CellAddress(3, 1))), source.Values);
        Assert.Equal(References.Write("Pivot", new CellRange(new CellAddress(1, 0), new CellAddress(3, 0))), source.Categories);
        var data = new ChartDataExtractor().Extract(workbook, ChartOf(workbook));
        Assert.Equal(["A", "B", "C"], data.Categories);
        Assert.Equal([1d, 2d, 3d], data.Series[0].Values);
    }

    [Fact]
    public void Renaming_the_pivot_keeps_the_chart_linked()
    {
        var workbook = WithChart();

        Assert.True(Coordinator.Apply(workbook, Definition(workbook, 2) with { Name = "Revenue" }, Labels).Succeeded);

        Assert.Equal("Revenue", ChartOf(workbook).PivotSource!.PivotName);
    }

    [Fact]
    public void Deleting_the_pivot_leaves_a_static_chart_over_its_last_values()
    {
        var workbook = WithChart();
        Assert.True(Coordinator.Apply(workbook, Definition(workbook, 2), Labels).Succeeded);

        Coordinator.Remove(workbook, "p1");

        var chart = ChartOf(workbook);
        Assert.Null(chart.PivotSource);
        Assert.Equal(["A", "B"], chart.InlineData!.Categories);
        Assert.Equal([1d, 2d], chart.InlineData.Series[0].Values);
        Assert.Equal(chart.InlineData, new ChartDataExtractor().Extract(workbook, chart));
    }
}
