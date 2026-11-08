using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class HeaderFooterResolverTests
{
    private static readonly DateTime When = new(2026, 9, 29);

    [Fact]
    public void NothingWhenAllOff()
    {
        var chrome = new HeaderFooterResolver().Resolve(new HeaderFooterSettings(),
            new Slide("s1", SlideLayout.TitleAndContent), 1, When);

        Assert.True(chrome.IsEmpty);
    }

    [Fact]
    public void NumberAndFooterResolve()
    {
        var settings = new HeaderFooterSettings
        {
            ShowSlideNumber = true,
            ShowFooter = true,
            FooterText = "Bozza",
        };

        var chrome = new HeaderFooterResolver().Resolve(settings,
            new Slide("s3", SlideLayout.TitleAndContent), 3, When);

        Assert.Equal("3", chrome.Number);
        Assert.Equal("Bozza", chrome.Footer);
        Assert.Null(chrome.Date);
    }

    [Fact]
    public void FixedDateWins()
    {
        var settings = new HeaderFooterSettings { ShowDate = true, DateIsFixed = true, FixedDate = "1 gennaio" };

        var chrome = new HeaderFooterResolver().Resolve(settings,
            new Slide("s1", SlideLayout.TitleAndContent), 1, When);

        Assert.Equal("1 gennaio", chrome.Date);
    }

    [Fact]
    public void TitleSlideHiddenUnlessRequested()
    {
        var settings = new HeaderFooterSettings { ShowSlideNumber = true };
        var title = new Slide("s1", SlideLayout.TitleSlide);

        Assert.True(new HeaderFooterResolver().Resolve(settings, title, 1, When).IsEmpty);

        var showing = settings with { ShowOnTitleSlide = true };
        Assert.Equal("1", new HeaderFooterResolver().Resolve(showing, title, 1, When).Number);
    }
}
