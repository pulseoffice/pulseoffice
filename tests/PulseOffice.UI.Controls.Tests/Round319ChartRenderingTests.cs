using Avalonia;
using PulseOffice.Charts;
using PulseOffice.UI.Controls.Charts;
using PulseOffice.UI.Controls.Charts.Plots;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public class Round319ChartRenderingTests
{
    private static readonly Rect Plot = new(0, 0, 300, 200);

    private static ChartFrame Frame(ChartType type, ChartData data) =>
        new(null!, new ChartDefinition { Id = "c", Type = type, SourceSheet = "S", SourceRange = "A1:C3" }, data, Plot, Plot);

    [Fact]
    public void A_stacked_area_fills_down_to_the_series_beneath_it()
    {
        var plot = new LinePlot();
        var frame = Frame(ChartType.StackedArea, new ChartData(["A", "B"], [new ChartSeries("Low", [1, 2]), new ChartSeries("High", [3, 3])]));
        var scale = plot.ValueAxis(frame)!;

        var outline = plot.AreaOutline(frame, 1);

        Assert.Equal(4, outline.Count);
        Assert.Equal(scale.Position(2), outline[2].Y, 3);
        Assert.Equal(scale.Position(1), outline[3].Y, 3);
    }

    [Fact]
    public void An_area_below_zero_fills_up_to_the_axis()
    {
        var plot = new LinePlot();
        var frame = Frame(ChartType.Area, new ChartData(["A", "B"], [new ChartSeries("S", [-4, -2])]));
        var scale = plot.ValueAxis(frame)!;

        var outline = plot.AreaOutline(frame, 0);

        Assert.Equal(scale.Position(0), outline[^1].Y, 3);
    }

    [Fact]
    public void Stacked_bar_labels_sit_in_the_middle_of_their_segment()
    {
        var plot = new BarPlot();
        var frame = Frame(ChartType.StackedColumn, new ChartData(["A"], [new ChartSeries("One", [2]), new ChartSeries("Two", [4])]));
        var scale = plot.ValueAxis(frame)!;

        var label = plot.DataLabels(frame).Single(candidate => candidate.Series == 1);

        Assert.Equal(ChartLabelPlacement.Centre, label.Placement);
        Assert.Equal(scale.Position(4), label.Middle!.Value.Y, 3);
        var origin = new ChartLabelLayout().Origin(label, new Size(10, 8));
        Assert.Equal(scale.Position(4) - 4, origin.Y, 3);
    }

    [Fact]
    public void Negative_columns_carry_their_label_below_and_bars_beside_the_end()
    {
        var data = new ChartData(["A", "B"], [new ChartSeries("S", [3, -3])]);
        var columns = new BarPlot().DataLabels(Frame(ChartType.Column, data)).ToList();
        var bars = new BarPlot().DataLabels(Frame(ChartType.Bar, data)).ToList();

        Assert.Equal([ChartLabelPlacement.Above, ChartLabelPlacement.Below], columns.Select(label => label.Placement));
        Assert.Equal([ChartLabelPlacement.After, ChartLabelPlacement.Before], bars.Select(label => label.Placement));
    }

    [Fact]
    public void Combination_column_labels_follow_the_drawn_column_slots_and_line_labels_the_category_centre()
    {
        var plot = new BarPlot();
        var frame = Frame(ChartType.ColumnLine, new ChartData(["A"], [new ChartSeries("Columns", [2]), new ChartSeries("Line", [3])]));
        var categories = new CategoryScale(1, Plot.X, Plot.Right);

        var labels = plot.DataLabels(frame).ToList();

        var (start, width) = categories.Slot(0, 0, 1);
        Assert.Equal(start + width / 2, labels.Single(label => label.Series == 0).Anchor.X, 3);
        Assert.Equal(categories.Centre(0), labels.Single(label => label.Series == 1).Anchor.X, 3);
    }

    [Theory]
    [InlineData(ChartType.PercentStackedColumn, true)]
    [InlineData(ChartType.PercentStackedArea, true)]
    [InlineData(ChartType.StackedColumn, false)]
    public void Hundred_percent_stacks_label_their_value_axis_in_percent(ChartType type, bool percent)
    {
        var definition = new ChartDefinition { Id = "c", Type = type, SourceSheet = "S", SourceRange = "A1:C3" };

        var format = new ChartRenderer().ValueFormat(definition, definition.ValueAxis);

        Assert.Equal(percent ? ChartRenderer.PercentStackFormat : null, format);
        Assert.Equal("100%", new ChartText(System.Globalization.CultureInfo.InvariantCulture).Formatted(100, ChartRenderer.PercentStackFormat));
    }
}

public class Round319PieLabelTests
{
    [Fact]
    public void Pie_labels_name_their_slice_so_category_and_percent_can_show()
    {
        var plot = new Rect(0, 0, 200, 200);
        var frame = new ChartFrame(null!, new ChartDefinition { Id = "p", Type = ChartType.Pie, SourceSheet = "S", SourceRange = "A1:B3" },
            new ChartData(["North", "South"], [new ChartSeries("S", [1, 3])]), plot, plot);

        var labels = new PiePlot().DataLabels(frame).ToList();

        Assert.Equal([0, 1], labels.Select(label => label.Index));
        Assert.All(labels, label => Assert.Equal(0, label.Series));
        Assert.All(labels, label => Assert.Equal(ChartLabelPlacement.Centre, label.Placement));
    }
}

public class Round319RadarScaleTests
{
    [Fact]
    public void Radar_rings_stand_on_the_value_axis_divisions_and_honour_a_fixed_maximum()
    {
        var plot = new Rect(0, 0, 200, 200);
        var data = new ChartData(["A", "B", "C"], [new ChartSeries("S", [1, 5, 9])]);
        var definition = new ChartDefinition { Id = "r", Type = ChartType.Radar, SourceSheet = "S", SourceRange = "A1:B4" };
        var radar = new RadarPlot();

        var automatic = radar.RingValues(new ChartFrame(null!, definition, data, plot, plot));
        var fixedTop = radar.RadialScale(new ChartFrame(null!, definition with { ValueAxis = new ChartAxisScale { Maximum = 20 } }, data, plot, plot))!;

        Assert.Equal(0, automatic[0]);
        Assert.Equal(10, automatic[^1]);
        Assert.Equal(20, fixedTop.Maximum);
    }
}

public class Round319ChartEditorAxesTests
{
    [Theory]
    [InlineData(ChartType.Pie, false)]
    [InlineData(ChartType.Treemap, false)]
    [InlineData(ChartType.Radar, false)]
    [InlineData(ChartType.Column, true)]
    [InlineData(ChartType.Scatter, true)]
    public void The_editor_knows_which_types_have_axes_to_edit(ChartType type, bool axes) =>
        Assert.Equal(axes, new ChartRenderer().DrawsAxes(type));
}
