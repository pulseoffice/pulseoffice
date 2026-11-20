using PulseOffice.Charts;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public class Round317ChartSelectionShapeTests
{
    private readonly ChartSelectionShape _shape = new();

    private static Worksheet Sheet(object?[,] cells)
    {
        var sheet = Workbook.CreateDefault().Sheets[0];
        for (var row = 0; row < cells.GetLength(0); row++)
        {
            for (var column = 0; column < cells.GetLength(1); column++)
            {
                switch (cells[row, column])
                {
                    case string text: sheet.SetValue(new CellAddress(row, column), CellValue.Text(text)); break;
                    case double number: sheet.SetValue(new CellAddress(row, column), CellValue.Number(number)); break;
                }
            }
        }
        return sheet;
    }

    private static CellRange Range(string a1) => CellRange.TryParseA1(a1, out var range) ? range : throw new ArgumentException(a1);

    [Fact]
    public void Numbers_only_make_every_column_a_series()
    {
        var layout = _shape.Detect(Sheet(new object?[,] { { 1.0, 2.0 }, { 3.0, 4.0 }, { 5.0, 6.0 } }), Range("A1:B3"));

        Assert.False(layout.FirstRowIsHeader);
        Assert.False(layout.FirstColumnIsCategory);
        Assert.Equal(2, layout.SeriesCount);
        Assert.Equal(LegendPosition.Right, layout.Legend(ChartType.Column));
    }

    [Fact]
    public void An_empty_corner_makes_numeric_years_a_header()
    {
        var layout = _shape.Detect(Sheet(new object?[,] { { null, 2024.0, 2025.0 }, { "Jan", 1.0, 2.0 }, { "Feb", 3.0, 4.0 }, { "Mar", 5.0, 6.0 } }), Range("A1:C4"));

        Assert.True(layout.FirstRowIsHeader);
        Assert.True(layout.FirstColumnIsCategory);
        Assert.True(layout.SeriesInColumns);
    }

    [Fact]
    public void A_wide_table_plots_series_in_rows_and_a_pie_keeps_its_legend()
    {
        var layout = _shape.Detect(Sheet(new object?[,] { { "Item", "Q1", "Q2", "Q3" }, { "Sales", 1.0, 2.0, 3.0 } }), Range("A1:D2"));

        Assert.False(layout.SeriesInColumns);
        Assert.Equal(1, layout.SeriesCount);
        Assert.Equal(LegendPosition.None, layout.Legend(ChartType.Column));
        Assert.Equal(LegendPosition.Right, layout.Legend(ChartType.Pie));
    }

    [Fact]
    public void A_single_cell_takes_its_current_region()
    {
        var layout = _shape.Detect(Sheet(new object?[,] { { "Month", "Sales" }, { "Jan", 1.0 }, { "Feb", 2.0 } }), Range("B2"));

        Assert.Equal("A1:B3", layout.Range.ToString());
        Assert.True(layout.FirstRowIsHeader);
        Assert.True(layout.FirstColumnIsCategory);
    }

    [Fact]
    public void A_scatter_takes_its_numeric_first_column_as_x()
    {
        var layout = _shape.Detect(Sheet(new object?[,] { { 1.0, 2.0 }, { 3.0, 4.0 }, { 5.0, 6.0 } }), Range("A1:B3")).For(ChartType.Scatter);

        Assert.True(layout.FirstColumnIsCategory);
        Assert.Equal(1, layout.SeriesCount);
        Assert.Equal(LegendPosition.None, layout.Legend(ChartType.Scatter));
    }
}
