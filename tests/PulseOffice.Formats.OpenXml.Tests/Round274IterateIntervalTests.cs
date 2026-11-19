using System.Xml.Linq;
using PulseOffice.Formats.Odf;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round274IterateIntervalTests
{
    private const string P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    [Fact]
    public void PercentIntervalIsReadAgainstTheElementDuration()
    {
        var head = XElement.Parse(
            $"<p:cTn xmlns:p=\"{P}\" id=\"5\"><p:iterate type=\"wd\"><p:tmPct val=\"10000\"/></p:iterate>"
            + "<p:childTnLst><p:set><p:cBhvr><p:cTn id=\"6\" dur=\"500\"/></p:cBhvr></p:set></p:childTnLst></p:cTn>");

        var behaviours = new PptxAnimationBehaviourCodec().Read(head);

        Assert.Equal("by-word", behaviours.IterateType);
        Assert.Equal(0.05, behaviours.IterateInterval!.Value, 6);
    }

    [Fact]
    public void AbsoluteIntervalStillReadsInMilliseconds()
    {
        var head = XElement.Parse($"<p:cTn xmlns:p=\"{P}\" id=\"5\"><p:iterate type=\"lt\"><p:tmAbs val=\"250\"/></p:iterate></p:cTn>");

        Assert.Equal(0.25, new PptxAnimationBehaviourCodec().Read(head).IterateInterval!.Value, 6);
    }

    [Theory]
    [InlineData("PT0.1S", 0.1)]
    [InlineData("0.1s", 0.1)]
    [InlineData("0.1", 0.1)]
    public void OdfIntervalAcceptsDurationsAndClockValues(string written, double seconds)
    {
        XNamespace anim = "urn:oasis:names:tc:opendocument:xmlns:animation:1.0";
        var iterate = new XElement(anim + "iterate",
            new XAttribute(anim + "iterate-type", "by-letter"), new XAttribute(anim + "iterate-interval", written));

        Assert.Equal(seconds, new OdfAnimationBehaviourCodec().Read(iterate).IterateInterval!.Value, 6);
    }
}
