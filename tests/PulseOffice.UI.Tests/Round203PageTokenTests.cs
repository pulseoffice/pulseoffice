using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.UI.Tests;

public sealed class Round203PageTokenTests
{
    private readonly WcagColorContrast _contrast = new();

    public static TheoryData<bool> Themes => new() { false, true };

    [Theory]
    [MemberData(nameof(Themes))]
    public void Page_previews_have_readable_ink_and_visible_guides_in_every_theme(bool dark)
    {
        var colors = Palette.Colors(dark);
        var page = colors[Palette.Keys.Page];

        Assert.True(_contrast.Ratio(colors[Palette.Keys.PageInk], page) >= 4.5);
        Assert.True(_contrast.Ratio(colors[Palette.Keys.PageGuide], page) >= 2.0);
        Assert.NotEqual(page, colors[Palette.Keys.PageSample]);
    }

    [Fact]
    public void The_dark_page_is_dimmer_than_the_light_one() =>
        Assert.True(_contrast.Ratio(Palette.Colors(true)[Palette.Keys.Page], Avalonia.Media.Colors.White) > 1.2);
}
