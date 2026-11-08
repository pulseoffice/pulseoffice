using System.Xml.Linq;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpSectionXml
{
    private readonly XName _section = XNamespace.Get("urn:pulseoffice:presentation-section") + "name";
    private readonly XName _sectionId = XNamespace.Get("urn:pulseoffice:presentation-section") + "id";

    public void Write(XElement page, Slide slide)
    {
        if (string.IsNullOrEmpty(slide.SectionName)) return;
        page.Add(new XAttribute(_section, slide.SectionName));
        if (slide.SectionId is { } id && Guid.TryParse(id, out _)) page.Add(new XAttribute(_sectionId, id));
    }

    public void Read(XElement page, Slide slide)
    {
        if ((string?)page.Attribute(_section) is not { Length: > 0 } name) return;
        slide.SectionName = name;
        slide.SectionId = (string?)page.Attribute(_sectionId) is { } id && Guid.TryParse(id, out _) ? id : null;
    }
}
