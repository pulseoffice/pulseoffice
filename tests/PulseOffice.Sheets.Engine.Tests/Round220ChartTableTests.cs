using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round220ChartTableTests
{
    private readonly ChartTableSources _tables = new();

    private static CellAddress A(string a1) => CellAddress.TryParseA1(a1, out var address) ? address : throw new ArgumentException(a1);

    private static (Workbook Workbook, ChartDefinition Chart) Scene()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet[A("A1")] = new Cell { Value = CellValue.Text("Region") };
        sheet[A("B1")] = new Cell { Value = CellValue.Text("Amount") };
        sheet[A("A2")] = new Cell { Value = CellValue.Text("North") };
        sheet[A("B2")] = new Cell { Value = CellValue.Number(10) };
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = new CellRange(A("A1"), A("B2")) });
        var chart = new ChartDefinition
        {
            Id = "chart1",
            Type = ChartType.Column,
            SourceSheet = sheet.Name,
            SourceRange = "A1:B2",
            SeriesSources = [new ChartSeriesSource(null, "Sales[Amount]", "Sales[Region]")],
        };
        sheet.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = A("D2") });
        return (workbook, chart);
    }

    [Fact]
    public void The_picker_lists_tables_with_their_columns_and_builds_references()
    {
        var (workbook, _) = Scene();

        var table = Assert.Single(_tables.Tables(workbook));
        Assert.Equal("Sales", table.Name);
        Assert.Equal(["Region", "Amount"], table.Columns);
        Assert.Equal("Sales[Amount]", _tables.Reference("Sales", "Amount"));
        Assert.Equal("Sales[Q'#1]", _tables.Reference("Sales", "Q#1"));
        Assert.Equal(new ChartTableReference("Sales", "Amount"), _tables.Describe("Sales[Amount]"));
        Assert.Null(_tables.Describe("Sheet1!A1:A4"));
    }

    [Fact]
    public void A_deleted_table_marks_the_chart_data_and_warns_once()
    {
        var (workbook, chart) = Scene();
        var watch = new ChartTableLossWatch(workbook);
        var warnings = new List<IReadOnlyList<string>>();
        watch.TablesLost += (_, lost) => warnings.Add(lost.Tables);

        Assert.False(new ChartDataExtractor().Extract(workbook, chart).LostTables);
        watch.Check();
        Assert.Empty(warnings);

        workbook.DatabaseRanges.Remove("Sales");
        watch.Check();
        watch.Check();

        Assert.Equal(["Sales"], Assert.Single(warnings));
        Assert.Equal(["Sales"], new ChartDataExtractor().Extract(workbook, chart).MissingTables);
    }
}
