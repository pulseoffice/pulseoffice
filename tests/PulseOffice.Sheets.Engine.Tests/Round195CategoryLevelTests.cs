using PulseOffice.Charts;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round195CategoryLevelTests
{
    [Fact]
    public void A_category_range_of_several_columns_reads_outer_levels()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        string[][] rows = [["2025", "Q1"], ["", "Q2"], ["2026", "Q1"]];
        for (var row = 0; row < rows.Length; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), CellValue.Text(rows[row][0]));
            sheet.SetValue(new CellAddress(row, 1), CellValue.Text(rows[row][1]));
            sheet.SetValue(new CellAddress(row, 2), CellValue.Number(row + 1));
        }
        var name = sheet.Name;
        var chart = new ChartDefinition
        {
            Id = "c", Type = ChartType.Column, SourceSheet = name, SourceRange = "A1:C3",
            SeriesSources = [new ChartSeriesSource(null, $"{name}!C1:C3", $"{name}!A1:B3")],
        };

        var data = new ChartDataExtractor().Extract(workbook, chart);

        Assert.Equal(["Q1", "Q2", "Q1"], data.Categories);
        Assert.Equal(["2025", "", "2026"], Assert.Single(data.CategoryLevels!));
        Assert.Equal([1d, 2d, 3d], Assert.Single(data.Series).Values);
    }
}
