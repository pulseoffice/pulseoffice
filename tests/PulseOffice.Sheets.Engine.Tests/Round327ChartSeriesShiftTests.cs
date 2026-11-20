using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round327ChartSeriesShiftTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private (Recalculation.CalculationEngine Engine, Worksheet Data) Scene()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var data = workbook.Sheets[0];
        var chart = new ChartDefinition
        {
            Id = "c", Type = ChartType.Column, SourceSheet = "Sheet1", SourceRange = "A1:D5",
            SeriesNames = ["First", "Second", "Third"],
            SeriesColors = ["#111111", "#222222", "#333333"],
        }.WithSeriesFormat(2, new ChartSeriesFormat { Marker = MarkerSymbol.Square });
        data.Objects.Add(new PlacedObject { Id = "c", Content = chart, Anchor = new CellAddress(1, 6) });
        return (engine, data);
    }

    private static ChartDefinition Chart(Worksheet sheet) => Assert.IsType<ChartDefinition>(sheet.Objects[0].Content);

    [Fact]
    public void A_column_inserted_between_series_moves_the_later_names_colours_and_formats()
    {
        var (engine, data) = Scene();

        new SheetStructure().InsertColumns(engine, 0, 2, 1);

        var chart = Chart(data);
        Assert.Equal("A1:E5", chart.SourceRange);
        Assert.Equal(new[] { "First", "", "Second", "Third" }, chart.SeriesNames!);
        Assert.Equal(new string?[] { "#111111", null, "#222222", "#333333" }, chart.SeriesColors!);
        Assert.Equal(MarkerSymbol.Square, chart.SeriesFormat(3).Marker);
        Assert.True(chart.SeriesFormat(2).IsPlain);
    }

    [Fact]
    public void Deleting_a_series_column_drops_its_settings_and_pulls_the_later_ones_back()
    {
        var (engine, data) = Scene();

        new SheetStructure().DeleteColumns(engine, 0, 1, 1);

        var chart = Chart(data);
        Assert.Equal(new[] { "Second", "Third" }, chart.SeriesNames!);
        Assert.Equal(new string?[] { "#222222", "#333333" }, chart.SeriesColors!);
        Assert.Equal(MarkerSymbol.Square, chart.SeriesFormat(1).Marker);
    }
}
