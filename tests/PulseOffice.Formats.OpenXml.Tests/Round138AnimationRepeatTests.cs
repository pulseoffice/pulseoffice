using System.Xml.Linq;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.OpenXml;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round138AnimationRepeatTests
{
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    private static XElement PptxEffect(string repeat) =>
        new(P + "cTn", new XAttribute("id", 5),
            new XElement(P + "childTnLst",
                new XElement(P + "animRot", new XAttribute("by", 21600000),
                    new XElement(P + "cBhvr",
                        new XElement(P + "cTn", new XAttribute("id", 6), new XAttribute("dur", 500), new XAttribute("repeatCount", repeat)),
                        new XElement(P + "tgtEl", new XElement(P + "spTgt", new XAttribute("spid", 4)))))));

    [Fact]
    public void Indefinite_repeat_read_from_pptx_reaches_odp()
    {
        var behaviours = new PptxAnimationBehaviourCodec().Read(PptxEffect("indefinite"));

        Assert.True(behaviours.Nodes.Single().Timing.RepeatsIndefinitely);

        var odf = new OdfAnimationBehaviourCodec().Write(behaviours, "id4");
        var repeat = odf.Descendants().Select(e => (string?)e.Attributes().FirstOrDefault(a => a.Name.LocalName == "repeatCount")).OfType<string>().Single();
        Assert.Equal("indefinite", repeat);

        var back = new OdfAnimationBehaviourCodec().Read(odf);
        Assert.True(back.Nodes.Single().Timing.RepeatsIndefinitely);
    }

    [Fact]
    public void Indefinite_repeat_survives_writing_pptx()
    {
        var behaviours = new AnimationBehaviours
        {
            Nodes = [new TransformBehaviour { TransformType = "rotate", By = "360", Timing = new BehaviourTiming { Duration = 0.5, RepeatsIndefinitely = true } }],
        };

        var written = new PptxAnimationBehaviourCodec().Children(behaviours, 4, new TimeNodeIdSequence(10)).Single();

        Assert.Equal("indefinite", (string?)written.Descendants(P + "cTn").Single().Attribute("repeatCount"));
    }

    [Fact]
    public void Enormous_repeat_count_does_not_overflow_in_pptx()
    {
        var behaviours = new AnimationBehaviours
        {
            Nodes = [new TransformBehaviour { TransformType = "rotate", By = "360", Timing = new BehaviourTiming { Duration = 0.5, RepeatCount = 1e12 } }],
        };

        var written = new PptxAnimationBehaviourCodec().Children(behaviours, 4, new TimeNodeIdSequence(10)).Single();

        Assert.True(long.Parse((string)written.Descendants(P + "cTn").Single().Attribute("repeatCount")!) > 0);
    }
}
