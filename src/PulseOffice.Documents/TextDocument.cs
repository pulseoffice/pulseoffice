using System.Globalization;
using PulseOffice.Document.Fonts;
using PulseOffice.Document.Forms;
using PulseOffice.Document.Metadata;
using PulseOffice.Document.Versions;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Security;

namespace PulseOffice.Documents;

public sealed class IdSequence
{
    private const long LargestReserved = long.MaxValue / 2;

    private readonly Dictionary<string, long> _counters = new(StringComparer.Ordinal);

    public string Next(string prefix)
    {
        var next = _counters.GetValueOrDefault(prefix) + 1;
        _counters[prefix] = next;
        return prefix + next.ToString(CultureInfo.InvariantCulture);
    }

    public void Reserve(string id)
    {
        var digits = id.Length - id.Reverse().TakeWhile(char.IsAsciiDigit).Count();
        if (digits == id.Length || digits == 0) return;
        var prefix = id[..digits];
        if (!long.TryParse(id[digits..], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number > LargestReserved) return;
        if (number > _counters.GetValueOrDefault(prefix)) _counters[prefix] = number;
    }

    public IdSequence Clone()
    {
        var copy = new IdSequence();
        foreach (var (prefix, value) in _counters) copy._counters[prefix] = value;
        return copy;
    }
}

public sealed class TextDocument : IFormDocument, IFontDocument, IMacroDocument, IMacroSignatureHolder
{
    public TextDocument() : this(new StyleSheet())
    {
    }

    public TextDocument(StyleSheet styles) => Styles = styles;

    public BlockContainer Body { get; } = new();

    public StyleSheet Styles { get; private set; }

    public AnchoredObjectCollection Objects { get; } = new();

    public CommentCollection Comments { get; } = new();

    public NoteCollection Notes { get; } = new();

    public TrackedChangeLog Changes { get; } = new();

    public Dictionary<string, string> Variables { get; } = new(StringComparer.Ordinal);

    public Fields.BibliographyCollection Bibliography { get; } = new();

    public IdSequence Ids { get; private set; } = new();

    public string InitialPageStyle { get; set; } = DefaultStyleNames.Standard;

    public DocumentProperties Properties { get; } = new();

    public string? Language
    {
        get => Properties.Language;
        set => Properties.Language = value;
    }

    public DocumentVersionCollection Versions { get; } = new();

    public IMacroLibrary Macros { get; } = new MacroLibrary();

    public MacroSignature? MacroSignature { get; set; }

    public FormDocumentSettings FormSettings { get; set; } = new();

    public Protection.DocumentProtection Protection { get; set; } = Documents.Protection.DocumentProtection.None;

    public Fields.MailMergeSource? MergeSource { get; set; }

    public DocumentFonts Fonts { get; set; } = DocumentFonts.Empty;

    public string? FileName { get; set; }

    public bool IsMaster { get; set; }

    public PulseOffice.Documents.Protection.MarkProtection Marks { get; set; } = PulseOffice.Documents.Protection.MarkProtection.None;

    public LineNumbering? LineNumbering { get; set; }

    public IReadOnlyList<(string Name, string Language, string Source)> SignableMacros =>
        [.. Macros.All.Select(macro => (macro.Name, macro.Language.ToString(), macro.Source))];

    public event EventHandler? Changed;

    public event EventHandler<TextChangedEventArgs>? TextChanged;

    public string NextId(string prefix) => Ids.Next(prefix);

    public long Revision { get; private set; }

    public void Raise()
    {
        Revision++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Raise(IReadOnlyList<TextChange> changes)
    {
        if (changes.Count > 0) TextChanged?.Invoke(this, new TextChangedEventArgs(changes));
        Raise();
    }

    public TextDocument Clone()
    {
        var copy = new TextDocument(Styles.Clone())
        {
            Ids = Ids.Clone(),
            InitialPageStyle = InitialPageStyle,
            MacroSignature = MacroSignature,
            FormSettings = FormSettings,
            Protection = Protection,
            MergeSource = MergeSource,
            Fonts = Fonts,
            FileName = FileName,
            IsMaster = IsMaster,
            Marks = Marks,
            LineNumbering = LineNumbering,
        };
        copy.Body.Replace(Body.Clone().Blocks);
        copy.Objects.CopyFrom(Objects);
        copy.Comments.CopyFrom(Comments);
        copy.Notes.CopyFrom(Notes);
        copy.Changes.CopyFrom(Changes);
        foreach (var (name, value) in Variables) copy.Variables[name] = value;
        copy.Bibliography.CopyFrom(Bibliography);
        copy.Properties.CopyFrom(Properties);
        foreach (var macro in Macros.All) copy.Macros.Store(macro);
        foreach (var version in Versions.All) copy.Versions.Adopt(version);
        return copy;
    }
}
