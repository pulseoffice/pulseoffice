using System.Globalization;
using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Charts;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round327ComboMarkerTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    [Fact]
    public Task Combination_line_markers_take_the_point_colour() => Session.Dispatch(() =>
    {
        var chart = new ChartDefinition
        {
            Id = "c",
            Type = ChartType.ColumnLine,
            SourceSheet = "Data",
            SourceRange = "A1:C3",
            Legend = LegendPosition.None,
        }.WithPointFormat(1, new ChartPointFormat(0) { ColorHex = "#123456" });
        var data = new ChartData(["A", "B"], [new ChartSeries("Bars", [3, 4]), new ChartSeries("Line", [2, 5])]);
        var group = new DrawingGroup();
        using (var context = group.Open())
            new ChartRenderer().Render(context, chart, data, new Rect(0, 0, 320, 200), ChartRenderMode.Full, CultureInfo.InvariantCulture, ChartInk.Print);
        var fills = new List<Color>();
        Collect(group, fills);

        Assert.Contains(Color.Parse("#123456"), fills);
        return Task.CompletedTask;
    }, CancellationToken.None);

    private static void Collect(Avalonia.Media.Drawing drawing, List<Color> fills)
    {
        switch (drawing)
        {
            case DrawingGroup group:
                foreach (var child in group.Children) Collect(child, fills);
                break;
            case GeometryDrawing { Brush: ISolidColorBrush brush }:
                fills.Add(brush.Color);
                break;
        }
    }
}
