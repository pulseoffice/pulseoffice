using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Presentations;
using P = DocumentFormat.OpenXml.Presentation;

namespace PulseOffice.Formats.OpenXml;

internal sealed class SlideIndex
{
    public Dictionary<string, string> ByRelationship { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> ByNumber { get; } = new(StringComparer.Ordinal);
}

internal interface IPptxPresentationPartReader
{
    void Read(PresentationPart part, Presentation deck, SlideIndex index);
}

internal abstract class PresentationXmlReader : IPptxPresentationPartReader
{
    public void Read(PresentationPart part, Presentation deck, SlideIndex index)
    {
        if (part.Presentation is not { } presentation) return;

        XDocument document;
        try { document = XDocument.Parse(presentation.OuterXml); }
        catch (System.Xml.XmlException) { return; }

        Read(document.Root!, deck, index);
    }

    protected abstract void Read(XElement root, Presentation deck, SlideIndex index);
}

internal sealed class SectionReader : PresentationXmlReader
{
    private static readonly XNamespace P14 = "http://schemas.microsoft.com/office/powerpoint/2010/main";

    protected override void Read(XElement root, Presentation deck, SlideIndex index)
    {
        foreach (var section in root.Descendants(P14 + "section"))
        {
            if ((string?)section.Attribute("name") is not { Length: > 0 } name) continue;

            var first = section.Descendants(P14 + "sldId")
                .Select(e => (string?)e.Attribute("id"))
                .FirstOrDefault(id => id is not null && index.ByNumber.ContainsKey(id));

            if (first is not null && deck.Slides.FirstOrDefault(s => s.Id == index.ByNumber[first]) is { } slide)
            {
                slide.SectionName = name;
                slide.SectionId = (string?)section.Attribute("id") is { } id && Guid.TryParse(id, out _) ? id : null;
            }
        }
    }
}

internal sealed class HeaderFooterReader : PresentationXmlReader
{
    protected override void Read(XElement root, Presentation deck, SlideIndex index)
    {
        if (new HeaderFooterXml().Parse(new HeaderFooterXml().Find(root)) is { } settings) deck.HeaderFooter = settings;
    }
}

internal sealed class NativeHeaderFooterReader : IPptxPresentationPartReader
{
    public void Read(PresentationPart part, Presentation deck, SlideIndex index)
    {
        if (deck.HeaderFooter.Any) return;
        if (new PptxHeaderFooter().Read(part.SlideParts) is { } settings) deck.HeaderFooter = settings;
    }
}

internal sealed class CustomShowReader : IPptxPresentationPartReader
{
    public void Read(PresentationPart part, Presentation deck, SlideIndex index)
    {
        foreach (var custom in part.Presentation?.GetFirstChild<P.CustomShowList>()?.Elements<P.CustomShow>() ?? [])
        {
            if (custom.Name?.Value is not { } name) continue;

            var ids = (custom.SlideList?.Elements<P.SlideListEntry>() ?? [])
                .Select(entry => entry.Id?.Value)
                .Where(relationship => relationship is not null && index.ByRelationship.ContainsKey(relationship))
                .Select(relationship => index.ByRelationship[relationship!])
                .ToList();

            if (ids.Count > 0) deck.AddCustomShow(new CustomShow(name, ids));
        }
    }
}

internal sealed class ShowPropertiesReader : IPptxPresentationPartReader
{
    public void Read(PresentationPart part, Presentation deck, SlideIndex index)
    {
        if (part.PresentationPropertiesPart?.PresentationProperties?.GetFirstChild<P.ShowProperties>() is not { } show) return;

        var range = show.GetFirstChild<P.SlideRange>();
        var pen = show.GetFirstChild<P.PenColor>()?.GetFirstChild<DocumentFormat.OpenXml.Drawing.RgbColorModelHex>()?.Val?.Value;

        deck.Setup = new ShowSetup
        {
            Loop = show.Loop?.Value ?? false,
            WithoutAnimation = show.ShowAnimation?.Value == false,
            Advance = show.UseTimings?.Value == false ? ShowAdvanceMode.Manual : ShowAdvanceMode.UseTimings,
            FromSlide = range?.Start?.Value is { } start ? (int)Math.Min(start, int.MaxValue) : 0,
            ToSlide = range?.End?.Value is { } end ? (int)Math.Min(end, int.MaxValue) : 0,
            PenColor = pen is { Length: 6 } ? $"#{pen.ToUpperInvariant()}" : "#E11D2E",
        };
    }
}

internal sealed class SlideListReader(PptxSlideReader slides, PptxDiagramExpander diagrams)
{
    public SlideIndex Read(PresentationPart part, Presentation deck)
    {
        var index = new SlideIndex();
        var count = 0;

        foreach (var entry in part.Presentation!.SlideIdList?.Elements<P.SlideId>() ?? [])
        {
            if (entry.RelationshipId?.Value is not { } relationship) continue;
            if (!part.TryGetPartById(relationship, out var found) || found is not SlidePart slidePart) continue;

            var id = $"slide{++count}";
            index.ByRelationship[relationship] = id;
            if (entry.Id?.Value is { } number) index.ByNumber[number.ToString(CultureInfo.InvariantCulture)] = id;
            var expanded = diagrams.Expand(slidePart);
            var groups = new PptxGroupFlattener().Flatten(slidePart.Slide?.CommonSlideData?.ShapeTree);
            deck.AddExisting(slides.Read(new PptxSlideSource(slidePart, part) { Groups = groups, Diagrams = expanded, Sounds = deck.Sounds }, id));
        }

        if (deck.Slides.Count == 0)
            deck.AddExisting(new SlideFactory().Create("slide1", deck.Master, SlideLayout.TitleSlide));

        return index;
    }
}
