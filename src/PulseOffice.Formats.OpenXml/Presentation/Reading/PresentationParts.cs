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

internal sealed class PptxLeadingSection
{
    public const string Id = "3A5F0C2E-8B1D-4E6A-9C7B-231D0DEF5EC7";

    public bool Is(string? id) => id is not null && Guid.TryParse(id, out var read) && read == Guid.Parse(Id);
}

internal sealed class SectionReader : PresentationXmlReader
{
    private static readonly XNamespace P14 = "http://schemas.microsoft.com/office/powerpoint/2010/main";

    private readonly PptxLeadingSection _leading = new();

    protected override void Read(XElement root, Presentation deck, SlideIndex index)
    {
        var pending = new List<(string Name, string? Id)>();
        var sections = root.Descendants(P14 + "section").ToList();
        for (var ordinal = 0; ordinal < sections.Count; ordinal++)
        {
            var section = sections[ordinal];
            var name = (string?)section.Attribute("name") ?? string.Empty;
            var id = (string?)section.Attribute("id") is { } raw && Guid.TryParse(raw, out _) ? raw : null;
            var first = section.Descendants(P14 + "sldId")
                .Select(e => (string?)e.Attribute("id"))
                .FirstOrDefault(number => number is not null && index.ByNumber.ContainsKey(number));
            var slide = first is null ? null : deck.Slides.FirstOrDefault(s => s.Id == index.ByNumber[first]);

            if (slide is null)
            {
                if (name.Length > 0) pending.Add((name, id));
                continue;
            }

            foreach (var empty in pending) deck.EmptySections.Add(new EmptySection(empty.Name, empty.Id, slide.Id));
            pending.Clear();
            if (ordinal == 0 && _leading.Is(id)) continue;
            if (name.Length == 0) continue;
            slide.SectionName = name;
            slide.SectionId = id;
        }
        foreach (var empty in pending) deck.EmptySections.Add(new EmptySection(empty.Name, empty.Id, null));
    }
}

internal sealed class FirstSlideNumberReader : IPptxPresentationPartReader
{
    public void Read(PresentationPart part, Presentation deck, SlideIndex index)
    {
        if (part.Presentation?.FirstSlideNum?.Value is { } first) deck.FirstSlideNumber = Math.Clamp(first, 0, 9999);
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
        var custom = show.ChildElements.FirstOrDefault(child => child.LocalName == "custShow")?
            .GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "id").Value;
        var customName = part.Presentation?.GetFirstChild<P.CustomShowList>()?.Elements<P.CustomShow>()
            .FirstOrDefault(entry => entry.Id?.Value.ToString(CultureInfo.InvariantCulture) == custom)?.Name?.Value;
        var pen = show.GetFirstChild<P.PenColor>()?.GetFirstChild<DocumentFormat.OpenXml.Drawing.RgbColorModelHex>()?.Val?.Value;

        deck.Setup = new ShowSetup
        {
            Loop = show.Loop?.Value ?? false,
            Kind = show.ChildElements.Any(child => child.LocalName == "kiosk") ? ShowKind.Kiosk
                : show.ChildElements.Any(child => child.LocalName == "browse") ? ShowKind.Window
                : ShowKind.Speaker,
            CustomShowName = custom is null ? null : customName,
            WithoutAnimation = show.ShowAnimation?.Value == false,
            Advance = show.UseTimings?.Value == false ? ShowAdvanceMode.Manual : ShowAdvanceMode.UseTimings,
            FromSlide = range?.Start?.Value is { } start ? (int)Math.Min(start, int.MaxValue) : 0,
            ToSlide = range?.End?.Value is { } end ? (int)Math.Min(end, int.MaxValue) : 0,
            PenColor = pen is { Length: 6 } ? $"#{pen.ToUpperInvariant()}" : "#E11D2E",
            KioskRestartMs = show.GetFirstChild<P.KioskSlideMode>()?.Restart?.Value is { } restart
                ? (int)Math.Min(restart, int.MaxValue) : ShowSetup.DefaultKioskRestartMs,
            LoopPauseMs = LoopPause(show),
        };
    }

    private int LoopPause(P.ShowProperties show)
    {
        var pause = show.Descendants().FirstOrDefault(element => element.LocalName == "loopPause" && element.NamespaceUri == ShowPropertiesWriter.ExtensionNamespace);
        var value = pause?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == "ms").Value;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) && ms > 0 ? ms : 0;
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
