using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round323ChartPointShiftTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private (Recalculation.CalculationEngine Engine, Worksheet Data) Scene(ChartDefinition chart)
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var data = workbook.Sheets[0];
        Assert.True(CellAddress.TryParseA1("E2", out var anchor));
        data.Objects.Add(new PlacedObject { Id = chart.Id, Content = chart, Anchor = anchor });
        return (engine, data);
    }

    private static ChartDefinition Compact() => new ChartDefinition
    {
        Id = "grafico1", Type = ChartType.Column, SourceSheet = "Sheet1", SourceRange = "A1:B5",
    }.WithPointFormat(0, new ChartPointFormat(1) { ColorHex = "#112233" }).WithPointFormat(0, new ChartPointFormat(3) { ColorHex = "#445566" });

    private static ChartDefinition Chart(Worksheet sheet) => Assert.IsType<ChartDefinition>(sheet.Objects[0].Content);

    [Fact]
    public void Rows_inserted_inside_the_data_move_the_later_point_formats_down()
    {
        var (engine, data) = Scene(Compact());

        new SheetStructure().InsertRows(engine, 0, 3, 2);

        var chart = Chart(data);
        Assert.Equal("A1:B7", chart.SourceRange);
        Assert.Equal("#112233", chart.PointFormat(0, 1).ColorHex);
        Assert.Equal("#445566", chart.PointFormat(0, 5).ColorHex);
        Assert.True(chart.PointFormat(0, 3).IsPlain);
    }

    [Fact]
    public void Rows_inserted_above_the_first_point_shift_every_point()
    {
        var (engine, data) = Scene(Compact());

        new SheetStructure().InsertRows(engine, 0, 1, 1);

        var chart = Chart(data);
        Assert.Equal("#112233", chart.PointFormat(0, 2).ColorHex);
        Assert.Equal("#445566", chart.PointFormat(0, 4).ColorHex);
    }

    [Fact]
    public void Deleting_a_formatted_point_row_drops_its_format_and_pulls_the_later_ones_up()
    {
        var (engine, data) = Scene(Compact());

        new SheetStructure().DeleteRows(engine, 0, 2, 1);

        var chart = Chart(data);
        Assert.Equal("#445566", chart.PointFormat(0, 2).ColorHex);
        Assert.True(chart.PointFormat(0, 1).IsPlain);
        Assert.Single(chart.SeriesFormat(0).Points.Items);
    }

    [Fact]
    public void Columns_inserted_beside_series_in_columns_keep_the_point_indices()
    {
        var (engine, data) = Scene(Compact());

        new SheetStructure().InsertColumns(engine, 0, 1, 1);

        Assert.Equal("#445566", Chart(data).PointFormat(1, 3).ColorHex);
        Assert.Equal("#112233", Chart(data).PointFormat(1, 1).ColorHex);
        Assert.True(Chart(data).SeriesFormat(0).IsPlain);
    }

    [Fact]
    public void Series_sources_shift_their_own_points()
    {
        var chart = Compact() with { SeriesSources = [new ChartSeriesSource(null, "Sheet1!B2:B5", null)] };
        var (engine, data) = Scene(chart);

        new SheetStructure().InsertRows(engine, 0, 2, 1);

        Assert.Equal("#112233", Chart(data).PointFormat(0, 2).ColorHex);
    }
}
