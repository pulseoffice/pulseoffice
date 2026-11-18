using Avalonia.Media;
using PulseOffice.UI.Controls.Charts;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Controls.Tests;

public sealed class Round217ChartMarkContrastTests
{
    private readonly WcagColorContrast _contrast = new();

    public static TheoryData<string> Hues => new() { "#16A34A", "#DC2626", "#DC143C", "#1F3A93", "#000000" };

    [Theory]
    [MemberData(nameof(Hues))]
    public void Marks_on_the_dark_page_keep_contrast(string hue)
    {
        var colours = Palette.Colors(true);
        var page = colours[Palette.Keys.Page];
        var ink = new ChartInk(new SolidColorBrush(colours[Palette.Keys.PageInk]), new SolidColorBrush(page));

        Assert.True(_contrast.Ratio(ink.Mark(Color.Parse(hue)), page) >= ChartInk.MarkContrast);
    }

    [Theory]
    [MemberData(nameof(Hues))]
    public void Print_marks_keep_their_hue(string hue) =>
        Assert.Equal(Color.Parse(hue), ChartInk.Print.Mark(Color.Parse(hue)));
}
