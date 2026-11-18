using System.Globalization;
using Avalonia;
using Avalonia.Media;
using PulseOffice.Charts;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Charts;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round208ChartInkTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private static readonly ChartDefinition Definition = new()
    {
        Id = "c",
        Type = ChartType.Column,
        SourceSheet = "Data",
        SourceRange = "A1:C3",
        ChartTitle = "Sales",
        CategoryAxisTitle = "Region",
        ShowDataLabels = true,
        Legend = LegendPosition.Right,
    };

    private static readonly ChartData Data = new(["North", "South"], [new ChartSeries("Units", [4, 7])]);

    private static List<Color> TextColours(Action<DrawingContext> paint)
    {
        var group = new DrawingGroup();
        using (var context = group.Open()) paint(context);
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
            case GlyphRunDrawing { Foreground: ISolidColorBrush brush }:
                colours.Add(brush.Color);
                break;
        }
    }

    [Fact]
    public Task Chart_text_takes_the_ink_the_host_passes() => Session.Dispatch(() =>
    {
        var ink = new ChartInk(new SolidColorBrush(Color.Parse("#F5F5F5")), new SolidColorBrush(Color.Parse("#1F2937")));

        var colours = TextColours(context => new ChartRenderer().Render(
            context, Definition, Data, new Rect(0, 0, 480, 300), ChartRenderMode.Full, CultureInfo.InvariantCulture, ink));

        Assert.NotEmpty(colours);
        Assert.All(colours, colour => Assert.Equal(Color.Parse("#F5F5F5"), colour));
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task Printing_and_export_keep_black_text_by_default() => Session.Dispatch(() =>
    {
        var colours = TextColours(context => new ChartRenderer().Render(
            context, Definition, Data, new Rect(0, 0, 480, 300), CultureInfo.InvariantCulture));

        Assert.NotEmpty(colours);
        Assert.All(colours, colour => Assert.Equal(Colors.Black, colour));
        return Task.CompletedTask;
    }, CancellationToken.None);
}
