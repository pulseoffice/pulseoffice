using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round216ChartTableSourceTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static CellAddress A(string a1) => CellAddress.TryParseA1(a1, out var address) ? address : throw new ArgumentException(a1);

    private (Workbook Workbook, Recalculation.CalculationEngine Engine, Worksheet Sheet) Scene()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var sheet = workbook.Sheets[0];
        sheet[A("A1")] = new Cell { Value = CellValue.Text("Region") };
        sheet[A("B1")] = new Cell { Value = CellValue.Text("Amount") };
        for (var row = 2; row <= 4; row++)
        {
            sheet[A($"A{row}")] = new Cell { Value = CellValue.Text("R" + row) };
            sheet[A($"B{row}")] = new Cell { Value = CellValue.Number(row * 10) };
        }
        workbook.DatabaseRanges.Define(new DatabaseRange { Name = "Sales", SheetIndex = 0, Range = new CellRange(A("A1"), A("B4")) });
        var chart = new ChartDefinition
        {
            Id = "chart1",
            Type = ChartType.Column,
            SourceSheet = sheet.Name,
            SourceRange = "A1:B4",
            SeriesSources = [new ChartSeriesSource("Sales[[#Headers],[Amount]]", "Sales[Amount]", "Sales[Region]")],
        };
        sheet.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = A("D2") });
        return (workbook, engine, sheet);
    }

    private static ChartDefinition Chart(Worksheet sheet) => Assert.IsType<ChartDefinition>(sheet.Objects[0].Content);

    [Fact]
    public void A_table_series_reads_the_table_and_grows_with_its_rows()
    {
        var (workbook, _, sheet) = Scene();

        var read = new ChartDataExtractor().Extract(workbook, Chart(sheet));
        Assert.Equal("Amount", Assert.Single(read.Series).Name);
        Assert.Equal([20d, 30d, 40d], read.Series[0].Values);
        Assert.Equal(["R2", "R3", "R4"], read.Categories);

        sheet[A("A5")] = new Cell { Value = CellValue.Text("R5") };
        sheet[A("B5")] = new Cell { Value = CellValue.Number(50) };
        var table = workbook.DatabaseRanges.Find("Sales")!;
        workbook.DatabaseRanges.Remove(table.Name);
        workbook.DatabaseRanges.Define(table with { Range = new CellRange(A("A1"), A("B5")) });

        Assert.Equal([20d, 30d, 40d, 50d], new ChartDataExtractor().Extract(workbook, Chart(sheet)).Series[0].Values);
    }

    [Fact]
    public void Renaming_the_table_renames_the_chart_series()
    {
        var (_, engine, sheet) = Scene();

        Assert.True(new TableRenaming().Rename(engine, "Sales", "Revenue"));

        var source = Assert.Single(Chart(sheet).SeriesSources!);
        Assert.Equal("Revenue[Amount]", source.Values);
        Assert.Equal("Revenue[Region]", source.Categories);
    }

    [Fact]
    public void Renaming_a_header_renames_the_chart_series()
    {
        var (_, engine, sheet) = Scene();

        engine.SetValue(new SheetCell(0, A("B1")), CellValue.Text("Total"));

        Assert.Equal("Sales[Total]", Assert.Single(Chart(sheet).SeriesSources!).Values);
    }

    [Fact]
    public void Flattening_writes_sheet_qualified_ranges()
    {
        var (workbook, _, sheet) = Scene();

        var flat = new ChartTableSources().Flatten(Chart(sheet), workbook);

        var source = Assert.Single(flat.SeriesSources!);
        Assert.Equal($"{sheet.Name}!B2:B4", source.Values);
        Assert.Equal($"{sheet.Name}!B1", source.Name);
    }
}
