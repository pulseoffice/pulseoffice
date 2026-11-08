using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf;

internal abstract class OdpXml : OdfXml;

internal sealed record PackageEntry(string Path, byte[] Data, string MediaType);

internal sealed record SubDocument(string Folder, XDocument Document, string MediaType);

internal sealed class OdpWriteSession(PulseOffice.Presentations.Presentation deck, VectorFallback vectors, System.Globalization.CultureInfo culture) : OdpXml, IOdfStyleSink
{
    public System.Globalization.CultureInfo Culture => culture;

    public VectorFallback Vectors => vectors;

    private int _styleCounter;
    private readonly List<PackageEntry> _media = [];
    private readonly List<SubDocument> _documents = [];
    private readonly List<(string Name, string Href)> _fillImages = [];
    private readonly Dictionary<string, int> _documentCounters = new(StringComparer.Ordinal);

    public PulseOffice.Presentations.Presentation Deck => deck;

    public XElement AutomaticStyles { get; } = new(Office + "automatic-styles");

    public OdfShapeStyleMapper ShapeStyles { get; } = new();

    public bool UsesListStyles { get; private set; }

    public IReadOnlyList<PackageEntry> Media => _media;

    public IReadOnlyList<OdfFontFile> FontFiles { get; set; } = [];

    public IReadOnlyList<SubDocument> Documents => _documents;

    public string NextStyle(string prefix) => $"{prefix}{++_styleCounter}";

    public IOdfTextStyleSink TextStyles => field ??= new OdfTextStyleSink(() => NextStyle("ST"), AddStyle);

    public void AddStyle(XElement style) => AutomaticStyles.Add(style);

    public void UseListStyles() => UsesListStyles = true;

    public void AddMedia(string path, byte[] data, string mediaType)
    {
        if (_media.All(entry => entry.Path != path)) _media.Add(new PackageEntry(path, data, mediaType));
    }

    public string AddOwnMedia(string path, byte[] data, string mediaType)
    {
        var dot = path.LastIndexOf('.');
        var slash = path.LastIndexOf('/');
        var (stem, extension) = dot > slash ? (path[..dot], path[dot..]) : (path, string.Empty);
        var candidate = path;
        for (var suffix = 2; _media.FirstOrDefault(entry => entry.Path == candidate) is { } taken; suffix++)
        {
            if (taken.Data.AsSpan().SequenceEqual(data)) return candidate;
            candidate = $"{stem}-{suffix}{extension}";
        }
        _media.Add(new PackageEntry(candidate, data, mediaType));
        return candidate;
    }

    public string AddDocument(string kind, XDocument document, string mediaType)
    {
        var number = _documentCounters[kind] = _documentCounters.GetValueOrDefault(kind) + 1;
        var folder = $"{kind} {number}";
        _documents.Add(new SubDocument(folder, document, mediaType));
        return $"./{folder}";
    }

    public string AddFillImage(string name, string href)
    {
        _fillImages.Add((name, href));
        return name;
    }

    public IEnumerable<XElement> FillImages() =>
        _fillImages.Select(image => new XElement(Draw + "fill-image",
            new XAttribute(Draw + "name", image.Name),
            new XAttribute(Xlink + "href", image.Href),
            new XAttribute(Xlink + "type", "simple"),
            new XAttribute(Xlink + "show", "embed"),
            new XAttribute(Xlink + "actuate", "onLoad")));

    private Dictionary<string, string>? _pageNames;

    public string PageName(string slideId)
    {
        _pageNames ??= UniquePageNames();
        return _pageNames.TryGetValue(slideId, out var name) ? name : slideId;
    }

    private Dictionary<string, string> UniquePageNames()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var slide in deck.Slides)
        {
            var candidate = SlideNames.PageName(slide);
            var unique = candidate;
            for (var suffix = 2; !used.Add(unique); suffix++) unique = $"{candidate} ({suffix})";
            names.TryAdd(slide.Id, unique);
        }

        return names;
    }
}
