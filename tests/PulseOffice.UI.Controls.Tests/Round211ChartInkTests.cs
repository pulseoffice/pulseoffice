using System.Globalization;
using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Charts;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round211ChartInkTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private static readonly Color Light = Color.Parse("#F5F5F5");

    private static readonly Color Dark = Color.Parse("#1F2937");

    private static readonly ChartInk Themed = new(new SolidColorBrush(Light), new SolidColorBrush(Dark));

    private static readonly ChartData Data = new(["North", "South", "East"], [new ChartSeries("Units", [-4, 7, 9]), new ChartSeries("Goal", [5, 8, 3])]);

    private static ChartDefinition Chart(ChartType type) => new()
    {
        Id = "c",
        Type = type,
        SourceSheet = "Data",
        SourceRange = "A1:C4",
        Legend = LegendPosition.None,
    };

    private static List<Color> StrokeColours(ChartType type, ChartInk ink)
    {
        var group = new DrawingGroup();
        using (var context = group.Open())
            new ChartRenderer().Render(context, Chart(type), Data, new Rect(0, 0, 480, 300), ChartRenderMode.Full, CultureInfo.InvariantCulture, ink);
        var colours = new List<Color>();
        Collect(group, colours);
        return colours;
    }

    private static void Collect(Avalonia.Media.Drawing drawing, List<Color> colours)
    {
        switch (drawing)
        {
            case DrawingGroup group:
                foreach (var child in group.Children) Collect(child, colours);
                break;
            case GeometryDrawing { Pen.Brush: ISolidColorBrush brush }:
                colours.Add(brush.Color);
                break;
        }
    }

    [Theory]
    [InlineData(ChartType.Column)]
    [InlineData(ChartType.BoxPlot)]
    [InlineData(ChartType.Bullet)]
    [InlineData(ChartType.Radar)]
    [InlineData(ChartType.Pie)]
    public Task Plot_marks_that_are_not_series_follow_the_host_ink(ChartType type) => Session.Dispatch(() =>
    {
        var colours = StrokeColours(type, Themed);

        Assert.NotEmpty(colours);
        Assert.DoesNotContain(Colors.Gray, colours);
        Assert.DoesNotContain(Colors.Black, colours);
        Assert.DoesNotContain(Colors.DimGray, colours);
        Assert.DoesNotContain(Colors.White, colours);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task Printing_keeps_dark_axis_lines_on_white() => Session.Dispatch(() =>
    {
        var colours = StrokeColours(ChartType.Column, ChartInk.Print);

        Assert.Contains(Colors.Black, colours);
        Assert.DoesNotContain(Light, colours);
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public void Stroke_and_grid_derive_from_the_text_ink()
    {
        Assert.Equal(Light, ((ISolidColorBrush)Themed.Stroke).Color);
        Assert.Equal(Light, ((ISolidColorBrush)Themed.Grid).Color);
        Assert.Equal(Dark, ((ISolidColorBrush)Themed.Separator).Color);
        Assert.True(Themed.Grid.Opacity < Themed.Stroke.Opacity);
    }
}
