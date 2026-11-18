using System.Xml.Linq;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpSectionXml
{
    private static readonly XNamespace Section = "urn:pulseoffice:presentation-section";

    private readonly XName _section = Section + "name";
    private readonly XName _sectionId = Section + "id";
    private readonly XName _empty = Section + "empty-section";
    private readonly XName _after = Section + "after-page";

    public void Write(XElement page, Slide slide, PulseOffice.Presentations.Presentation deck)
    {
        foreach (var empty in deck.EmptySections.Where(section => section.BeforeSlideId == slide.Id))
            page.Add(Empty(empty, false));
        if (deck.Slides.Count > 0 && ReferenceEquals(deck.Slides[^1], slide))
        {
            var ids = deck.Slides.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var empty in deck.EmptySections.Where(section => section.BeforeSlideId is null || !ids.Contains(section.BeforeSlideId)))
                page.Add(Empty(empty, true));
        }

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

    public void ReadEmpty(IEnumerable<XElement> pages, PulseOffice.Presentations.Presentation deck)
    {
        var index = 0;
        foreach (var page in pages)
        {
            var slide = index < deck.Slides.Count ? deck.Slides[index] : null;
            index++;
            foreach (var empty in page.Elements(_empty))
            {
                if ((string?)empty.Attribute(_section) is not { Length: > 0 } name) continue;
                var id = (string?)empty.Attribute(_sectionId) is { } raw && Guid.TryParse(raw, out _) ? raw : null;
                var after = (string?)empty.Attribute(_after) == "true";
                deck.EmptySections.Add(new EmptySection(name, id, after ? null : slide?.Id));
            }
        }
    }

    private XElement Empty(EmptySection section, bool afterPage)
    {
        var element = new XElement(_empty, new XAttribute(_section, section.Name));
        if (section.Id is { } id && Guid.TryParse(id, out _)) element.Add(new XAttribute(_sectionId, id));
        if (afterPage) element.Add(new XAttribute(_after, "true"));
        return element;
    }
}
