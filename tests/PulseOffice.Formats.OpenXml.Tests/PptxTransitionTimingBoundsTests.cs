using PulseOffice.Formats.OpenXml;
using Xunit;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class PptxTransitionTimingBoundsTests
{
    private readonly PptxTransitionMapper _mapper = new();

    [Theory]
    [InlineData("2147483647", 3_600_000)]
    [InlineData("-5000", 0)]
    [InlineData("4000", 4000)]
    public void AdvanceAfterTimeFromAFileIsBoundedToOneHour(string value, int expected)
    {
        var read = _mapper.FromTransition(new P.Transition(new P.FadeTransition()) { AdvanceAfterTime = value });

        Assert.Equal(expected, read!.AdvanceAfterMs);
    }

    [Fact]
    public void AnExtremeTransitionDurationIsBoundedToOneHour()
    {
        var transition = new P.Transition(new P.FadeTransition());
        transition.SetAttribute(new DocumentFormat.OpenXml.OpenXmlAttribute("p14", "dur", "http://schemas.microsoft.com/office/powerpoint/2010/main", "2147483647"));
        var slide = new P.Slide(new P.CommonSlideData(new P.ShapeTree()), transition);

        Assert.Equal(3_600_000, _mapper.FromSlide(slide)!.DurationMs);
    }
}
