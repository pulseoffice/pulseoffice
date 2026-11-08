using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxAnimationSound
{
    private readonly XNamespace _p = "http://schemas.openxmlformats.org/presentationml/2006/main";
    private readonly XNamespace _r = PptxSounds.Relationships;

    public XElement Build(PptxSoundReference sound, int id) =>
        new(_p + "subTnLst",
            new XElement(_p + "audio",
                new XElement(_p + "cMediaNode", new XAttribute("vol", "80000"),
                    new XElement(_p + "cTn",
                        new XAttribute("id", id.ToString(CultureInfo.InvariantCulture)),
                        new XAttribute("display", "0"),
                        new XAttribute("masterRel", "sameClick"),
                        new XElement(_p + "stCondLst", new XElement(_p + "cond", new XAttribute("delay", "0"))),
                        new XElement(_p + "endCondLst",
                            new XElement(_p + "cond", new XAttribute("evt", "onNext"), new XAttribute("delay", "0"),
                                new XElement(_p + "tgtEl", new XElement(_p + "sldTgt"))))),
                    new XElement(_p + "tgtEl",
                        new XElement(_p + "sndTgt",
                            new XAttribute(XNamespace.Xmlns + "r", _r.NamespaceName),
                            new XAttribute(_r + "embed", sound.RelationshipId),
                            new XAttribute("name", sound.Name))))));

    public void Strip(XElement par)
    {
        foreach (var list in par.Descendants(_p + "subTnLst").Where(list => list.Descendants(_p + "sndTgt").Any()).ToList())
            list.Remove();
    }

    public OpenXmlElement? Target(P.CommonTimeNode effect) =>
        effect.GetFirstChild<P.SubTimeNodeList>()?.Descendants().FirstOrDefault(element => element.LocalName == "sndTgt");
}
