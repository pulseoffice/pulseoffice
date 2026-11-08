using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxIdTable
{
    private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);
    private int _next;

    public DocxIdTable(int first) => _next = first;

    public int this[string key]
    {
        get
        {
            if (!_ids.TryGetValue(key, out var id))
            {
                id = _next++;
                _ids[key] = id;
            }

            return id;
        }
    }

    public IReadOnlyDictionary<string, int> All => _ids;

    public int Fresh() => _next++;
}

public sealed class DocxWriteContext(TextDocument document, OpenXmlPart part)
{
    public TextDocument Document { get; } = document;

    public int Watermarks { get; set; }

    public OpenXmlPart Part { get; set; } = part;

    public DocxStyleMap Styles { get; } = new();

    public DocxListMap Lists { get; } = new();

    public DocxIdTable Bookmarks { get; } = new(0);

    public DocxIdTable Comments { get; } = new(0);

    public DocxIdTable Footnotes { get; } = new(1);

    public DocxIdTable Endnotes { get; } = new(1);

    public DocxIdTable Paragraphs { get; } = new(0x10000001);

    public DocxIdTable Drawings { get; } = new(1);

    public DocxIdTable Revisions { get; } = new(100000);

    public HashSet<string> PendingPageObjects { get; } = new(StringComparer.Ordinal);

    public HashSet<string> FloatingTables { get; } = new(StringComparer.Ordinal);

    public HashSet<string> WrittenPageStyles { get; } = new(StringComparer.Ordinal);

    public IReadOnlySet<string>? ReferencedNames { get; set; }

    public HashSet<string> BlockControls { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, DocumentFormat.OpenXml.Wordprocessing.Paragraph> WrittenParagraphs { get; } = new(StringComparer.Ordinal);

    public string? OpenInsertion { get; set; }

    public Dictionary<string, PulseOffice.Documents.Annotations.TrackedChange> SplitChanges { get; } = new(StringComparer.Ordinal);

    public PulseOffice.Documents.Annotations.TrackedChange? Change(string id) => SplitChanges.GetValueOrDefault(id) ?? Document.Changes.Find(id);

    public string? OpenDeletion { get; set; }

    public IReadOnlySet<string>? BalancedFields { get; set; }

    public HashSet<string> OpenFields { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, Stack<bool>> FieldNesting { get; } = new(StringComparer.Ordinal);

    private readonly Dictionary<string, Queue<string>> _indexRanges = new(StringComparer.Ordinal);

    private int _indexRangeCount;

    public string OpenIndexRange(string key)
    {
        var name = "_Idx" + (++_indexRangeCount).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!_indexRanges.TryGetValue(key, out var open)) _indexRanges[key] = open = new Queue<string>();
        open.Enqueue(name);
        return name;
    }

    public string? CloseIndexRange(string key) =>
        _indexRanges.TryGetValue(key, out var open) && open.TryDequeue(out var name) ? name : null;

    public ImagePart AddImage(PartTypeInfo type) => Part switch
    {
        MainDocumentPart main => main.AddImagePart(type),
        HeaderPart header => header.AddImagePart(type),
        FooterPart footer => footer.AddImagePart(type),
        FootnotesPart footnotes => footnotes.AddImagePart(type),
        EndnotesPart endnotes => endnotes.AddImagePart(type),
        WordprocessingCommentsPart comments => comments.AddImagePart(type),
        _ => throw new InvalidOperationException("Images cannot be stored in " + Part.Uri),
    };

    private readonly Dictionary<(OpenXmlPart, string), string> _links = [];

    public string Link(string target)
    {
        if (_links.TryGetValue((Part, target), out var id)) return id;
        id = Part.AddHyperlinkRelationship(Address(target), true).Id;
        _links[(Part, target)] = id;
        return id;
    }

    private Uri Address(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var absolute)) return absolute;
        if (Uri.TryCreate(target, UriKind.Relative, out var relative)) return relative;
        var escaped = new System.Text.StringBuilder();
        foreach (var rune in target.EnumerateRunes())
        {
            if (rune.Value is > 32 and < 127 && "\"<>\\^`{|}".IndexOf((char)rune.Value) < 0) escaped.Append((char)rune.Value);
            else foreach (var octet in System.Text.Encoding.UTF8.GetBytes(rune.ToString())) escaped.Append('%').Append(octet.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }
        return Uri.TryCreate(escaped.ToString(), UriKind.Absolute, out var repaired) ? repaired
            : new Uri(Uri.EscapeDataString(target), UriKind.Relative);
    }
}

public sealed record DocxTextBoxLink(uint Chain, uint Sequence, string ObjectId);

public sealed class DocxReadContext(TextDocument document, OpenXmlPart part)
{
    public TextDocument Document { get; } = document;

    public PulseOffice.Documents.Styles.PageWatermark? Watermark { get; set; }

    public DocxFieldRangeState FieldRanges { get; } = new();

    public OpenXmlPart Part { get; set; } = part;

    public DocxStyleMap Styles { get; } = new();

    public DocxListMap Lists { get; } = new();

    public Dictionary<string, string> Bookmarks { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Comments { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Footnotes { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> Endnotes { get; } = new(StringComparer.Ordinal);

    public HashSet<string> AnchoredComments { get; } = new(StringComparer.Ordinal);

    public HashSet<string> ReferencedBookmarks { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> CarrierIds { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> CarrierStarts { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> CarrierEnds { get; } = new(StringComparer.Ordinal);

    public List<(PulseOffice.Documents.Marks.IndexEntryMark Entry, string Bookmark)> IndexRanges { get; } = [];

    public Stack<DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties?> TableParagraphDefaults { get; } = new();

    public HashSet<string> Losses { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, DocumentFormat.OpenXml.Wordprocessing.FrameProperties?> StyleFrames { get; } = new(StringComparer.Ordinal);

    public PulseOffice.Documents.Annotations.Insertion? OpenInsertion { get; set; }

    public List<DocxTextBoxLink> TextBoxLinks { get; } = [];

    public PulseOffice.Formats.ImportedCellBudget TableCells { get; } = new(PulseOffice.Formats.ImportedTableLimits.DocumentTableCells);

    private readonly HyperlinkTargets _hyperlinks = new();

    public string? Link(string relationshipId) => _hyperlinks.Of(Part, relationshipId);
}
