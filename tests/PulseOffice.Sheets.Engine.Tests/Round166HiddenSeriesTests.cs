using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round166HiddenSeriesTests
{
    [Fact]
    public void A_hidden_series_column_does_not_hand_its_name_and_colour_to_the_next_one()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.SetValue(new CellAddress(0, 0), CellValue.Number(1));
        sheet.SetValue(new CellAddress(0, 1), CellValue.Number(2));
        sheet.Layout.HideColumn(0);
        var definition = new ChartDefinition
        {
            Id = "c1",
            Type = ChartType.Column,
            SourceSheet = sheet.Name,
            SourceRange = "A1:B1",
            FirstRowIsHeader = false,
            FirstColumnIsCategory = false,
            SeriesNames = ["First", "Second"],
            SeriesColors = ["#111111", "#222222"],
        };

        var data = new ChartDataExtractor().Extract(workbook, definition, visibleOnly: true);

        var series = Assert.Single(data.Series);
        Assert.Equal("Second", series.Name);
        Assert.Equal("#222222", series.ColorHex);
    }
}
