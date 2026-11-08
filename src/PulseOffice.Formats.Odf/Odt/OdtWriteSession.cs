using System.Globalization;
using System.Xml.Linq;
using PulseOffice.Documents;

namespace PulseOffice.Formats.Odf.Odt;

internal sealed class OdtStyleSink(string prefix) : OdtXml
{
    private readonly Dictionary<string, string> _known = new(StringComparer.Ordinal);
    private int _counter;

    public List<XElement> Styles { get; } = [];

    public string Register(string family, string? parent, IEnumerable<object?> content, string? namePrefix = null)
    {
        var body = new XElement(Style + "style",
            new XAttribute(Style + "family", family),
            parent is null ? null : new XAttribute(Style + "parent-style-name", parent),
            content);
        var key = body.ToString(SaveOptions.DisableFormatting);
        if (_known.TryGetValue(key, out var existing)) return existing;

        var name = prefix + (namePrefix ?? family[..1].ToUpperInvariant()) + (++_counter).ToString(CultureInfo.InvariantCulture);
        body.ReplaceAttributes(new object[] { new XAttribute(Style + "name", name) }.Concat(body.Attributes().ToList()));
        Styles.Add(body);
        _known[key] = name;
        return name;
    }

    public void Add(XElement style) => Styles.Add(style);
}

internal sealed record OdtMediaEntry(string Path, byte[] Data, string MediaType);

internal sealed class OdtWriteSession(TextDocument document)
{
    private int _note;
    private readonly Dictionary<string, string> _indexIds = new(StringComparer.Ordinal);
    private int _index;

    public TextDocument Document { get; } = document;

    public Dictionary<PulseOffice.Documents.Blocks.CellNumberFormat, string> DataStyles { get; } = [];

    private HashSet<string>? _sequenceReferences;

    public bool IsSequenceReference(string name, Func<TextDocument, IEnumerable<string>> scan)
    {
        _sequenceReferences ??= new HashSet<string>(scan(Document), StringComparer.Ordinal);
        return _sequenceReferences.Contains(name);
    }

    public string? ImpliedInitialPage { get; set; }

    public OdtDrawingSession Drawing { get; } = new(new PulseOffice.Text.Rich.TextCultures(System.Globalization.CultureInfo.InvariantCulture).For(document.Language));

    public OdtStyleSink ContentStyles { get; } = new(string.Empty);

    public OdtStyleSink HeaderStyles { get; } = new("M");

    public HashSet<string> RegionChanges { get; } = new(StringComparer.Ordinal);

    public List<OdtMediaEntry> Media { get; } = [];

    public HashSet<string> Fonts { get; } = new(StringComparer.OrdinalIgnoreCase);

    public OdtListContinuity Lists { get; } = new();

    public void AddMedia(string path, byte[] data, string mediaType)
    {
        if (Media.All(entry => entry.Path != path)) Media.Add(new OdtMediaEntry(path, data, mediaType));
    }

    public int NextNote() => ++_note;

    public string IndexId(string key, bool opening)
    {
        if (opening || !_indexIds.TryGetValue(key, out var id))
        {
            id = "IMark" + (++_index).ToString(CultureInfo.InvariantCulture);
            _indexIds[key] = id;
        }
        return id;
    }
}
