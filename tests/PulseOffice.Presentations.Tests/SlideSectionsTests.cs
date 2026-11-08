using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class SlideSectionsTests
{
    private static Slide Slide(string id, string? section = null) =>
        new(id, SlideLayout.Blank) { SectionName = section };

    [Fact]
    public void GroupsConsecutiveSlidesUnderTheStartMarker()
    {
        var slides = new[]
        {
            Slide("a", "Intro"),
            Slide("b"),
            Slide("c", "Body"),
            Slide("d"),
            Slide("e"),
        };

        var sections = new SlideSections().Group(slides);

        Assert.Equal(2, sections.Count);
        Assert.Equal("Intro", sections[0].Name);
        Assert.Equal(2, sections[0].Slides.Count);
        Assert.Equal("Body", sections[1].Name);
        Assert.Equal(3, sections[1].Slides.Count);
    }

    [Fact]
    public void SlidesBeforeTheFirstMarkerAreUnsectioned()
    {
        var slides = new[] { Slide("a"), Slide("b", "Body"), Slide("c") };

        var sections = new SlideSections().Group(slides);

        Assert.Single(sections);
        Assert.Equal("Body", sections[0].Name);
        Assert.Equal(2, sections[0].Slides.Count);
    }

    [Fact]
    public void StartOfWalksBackToTheNearestMarker()
    {
        var start = Slide("a", "Intro");
        var slides = new[] { start, Slide("b"), Slide("c") };

        Assert.Same(start, new SlideSections().StartOf(slides, slides[2]));
    }

    [Fact]
    public void StartOfReturnsNullWhenNoSectionPrecedes()
    {
        var slides = new[] { Slide("a"), Slide("b", "Body") };

        Assert.Null(new SlideSections().StartOf(slides, slides[0]));
    }
}
