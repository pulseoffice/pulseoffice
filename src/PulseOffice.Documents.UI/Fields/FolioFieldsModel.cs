using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.UI.Commands;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.UI.Fields;

public sealed record FolioReferenceKind(ReferenceTargetKind Kind, string? Sequence, string Title, string Icon);

public sealed record FolioReferenceFormat(string Key, string Format, string Title);

public enum FolioFieldsPage
{
    Fields,
    References,
}

public sealed record FolioFieldsStart(FolioFieldsPage Page)
{
    public FieldHit? Editing { get; init; }

    public FolioFieldChoice? Choice { get; init; }

    public FolioReferenceKind? ReferenceKind { get; init; }

    public ReferenceTarget? Target { get; init; }

    public string? Format { get; init; }
}

public sealed record FolioFieldsServices(
    FolioFieldCatalog Catalog,
    IReadOnlyList<FolioFieldOptions> Options,
    IFieldUpdater Updater,
    IFolioFieldRefresh Refresh,
    IReferenceTargets Targets,
    IBookmarkCatalog Bookmarks,
    IDocumentReadingOrder Order,
    IFolioFieldInsertion Insertion,
    ITextCatalog Texts,
    TimeProvider Clock);

public sealed class FolioFieldsModel
{
    private readonly FolioEditor _editor;
    private readonly FolioFieldsServices _services;
    private readonly IReadOnlyList<FolioReferenceFormat> _formats;

    public FolioFieldsModel(FolioEditor editor, FolioFieldsServices services, FolioFieldsStart start)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(start);
        _editor = editor;
        _services = services;
        Start = start;
        _formats =
        [
            Format("text", ReferenceFormats.Text), Format("page", ReferenceFormats.Page), Format("number", ReferenceFormats.Number),
            Format("direction", ReferenceFormats.Direction), Format("chapter", ReferenceFormats.Chapter), Format("caption", ReferenceFormats.Caption),
            Format("category", ReferenceFormats.Category), Format("value", ReferenceFormats.Value),
        ];
        Context = new FieldOptionsContext(editor, services.Texts, services.Clock.GetLocalNow().DateTime);
    }

    public FolioFieldsStart Start { get; }

    public bool IsEditing => Start.Editing is not null;

    public FieldOptionsContext Context { get; }

    public ITextCatalog Texts => _services.Texts;

    public IReadOnlyList<FolioFieldCategory> Categories => _services.Catalog.Categories;

    public string CategoryTitle(FolioFieldCategory category) => Texts["folio.field.category." + category.Key];

    public IReadOnlyList<FolioFieldChoice> Fields(FolioFieldCategory category, string filter)
    {
        var trimmed = filter.Trim();
        return [.. _services.Catalog.Choices.Where(choice => trimmed.Length > 0
            ? Title(choice).Contains(trimmed, StringComparison.CurrentCultureIgnoreCase)
            : choice.Category == category.Key)];
    }

    public string Title(FolioFieldChoice choice) => Texts["folio.field.kind." + choice.Key];

    public FolioFieldOptions OptionsFor(FolioFieldChoice choice) => _services.Options.First(options => options.Handles(choice));

    public TextRun Seed(FolioFieldChoice choice) =>
        Start.Editing is { } hit && _services.Catalog.Of(hit.Run) == choice
            ? hit.Run
            : new TextRun(string.Empty) { Field = choice.Kind, FieldArgument = choice.Argument == "user" ? null : choice.Argument };

    public TextRun? Compose(FolioFieldChoice choice, IReadOnlyDictionary<string, object> values) =>
        OptionsFor(choice).Compose(choice, Seed(choice), values, Context);

    public string Preview(FolioFieldChoice choice, IReadOnlyDictionary<string, object> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (Compose(choice, values) is not { } run) return string.Empty;
        if (OptionsFor(choice).PreviewOverride(values) is { } overridden) return overridden;
        if (run.Field == TextFieldKind.HiddenParagraph) return string.Empty;
        try
        {
            return _services.Updater.Preview(_editor.Document, run with { Text = string.Empty }, Request(), _editor.Caret);
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        {
            return string.Empty;
        }
    }

    public bool Apply(FolioFieldChoice choice, IReadOnlyDictionary<string, object> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (Compose(choice, values) is not { } run || Refuses()) return false;
        var options = OptionsFor(choice);
        using (_editor.History.BeginTransaction(Start.Editing is null ? "insert-field" : "edit-field"))
        {
            options.Commit(run, values, Context);
            if (Start.Editing is { } hit) _services.Insertion.Replace(_editor, hit, run);
            else _services.Insertion.Insert(_editor, run);
        }
        return true;
    }

    public IReadOnlyList<FolioReferenceKind> ReferenceKinds()
    {
        var document = _editor.Document;
        var kinds = new List<FolioReferenceKind>
        {
            new(ReferenceTargetKind.Headings, null, Texts["folio.reference.kind.headings"], "link.place.heading"),
            new(ReferenceTargetKind.Bookmarks, null, Texts["folio.reference.kind.bookmarks"], "link.place.bookmark"),
        };
        kinds.AddRange(_services.Targets.Sequences(document).Select(sequence =>
            new FolioReferenceKind(ReferenceTargetKind.Captions, sequence, Texts.Format("folio.reference.kind.captions", sequence), "folio.reference.caption")));
        kinds.Add(new(ReferenceTargetKind.Footnotes, null, Texts["folio.reference.kind.footnotes"], "folio.reference.footnote"));
        kinds.Add(new(ReferenceTargetKind.Endnotes, null, Texts["folio.reference.kind.endnotes"], "folio.reference.endnote"));
        return kinds;
    }

    public IReadOnlyList<ReferenceTarget> Targets(FolioReferenceKind kind, string filter)
    {
        ArgumentNullException.ThrowIfNull(kind);
        var trimmed = filter.Trim();
        return [.. _services.Targets.Targets(_editor.Document, kind.Kind, kind.Sequence)
            .Where(target => trimmed.Length == 0 || target.Title.Contains(trimmed, StringComparison.CurrentCultureIgnoreCase))];
    }

    public IReadOnlyList<FolioReferenceFormat> Formats(FolioReferenceKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        var notes = kind.Kind is ReferenceTargetKind.Footnotes or ReferenceTargetKind.Endnotes;
        var formats = notes ? _formats.Where(entry => entry.Format is ReferenceFormats.Text or ReferenceFormats.Page or ReferenceFormats.Direction or ReferenceFormats.Chapter) : _formats;
        if (kind.Kind != ReferenceTargetKind.Captions) formats = formats.Where(entry => entry.Format is not (ReferenceFormats.Caption or ReferenceFormats.Category or ReferenceFormats.Value));
        return [.. formats];
    }

    public string DefaultFormat(FolioReferenceKind kind) => kind.Kind == ReferenceTargetKind.Captions ? ReferenceFormats.Category : ReferenceFormats.Text;

    public string ReferencePreview(ReferenceTarget target, string format)
    {
        ArgumentNullException.ThrowIfNull(target);
        var title = Flat(target.Title);
        var paragraph = ParagraphOf(target);
        var facts = SafeFacts();
        var notes = target.Kind is ReferenceTargetKind.Footnotes or ReferenceTargetKind.Endnotes;
        var noteNumber = notes ? title.Split('.', 2)[0] : null;
        return format switch
        {
            ReferenceFormats.Page => paragraph is not null && facts?.PageOf(paragraph) is { } page ? page : "1",
            ReferenceFormats.Direction => Direction(paragraph),
            ReferenceFormats.Number or ReferenceFormats.Chapter => paragraph is not null && facts?.ListLabel(paragraph) is { Length: > 0 } label ? label.Trim() : noteNumber ?? "1",
            ReferenceFormats.Category => title.Split(':', 2)[0].Trim(),
            ReferenceFormats.Value => new string(title.Split(':', 2)[0].Where(char.IsDigit).ToArray()) is { Length: > 0 } digits ? digits : "1",
            _ => noteNumber ?? title,
        };
    }

    public bool ApplyReference(ReferenceTarget target, string format)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Refuses()) return false;
        return Start.Editing is { } hit
            ? _services.Insertion.ReplaceReference(_editor, hit, target, format)
            : _services.Insertion.InsertReference(_editor, target, format);
    }

    private bool Refuses() => Start.Editing is { } hit ? _editor.RefusesEdit(hit.ParagraphId) : _editor.RefusesEdit();

    public string Flat(string text)
    {
        var flat = (text ?? string.Empty).ReplaceLineEndings(" ").Trim();
        return flat.Length <= 80 ? flat : flat[..80] + "…";
    }

    private FolioReferenceFormat Format(string key, string format) => new(key, format, Texts["folio.reference.format." + key]);

    private FieldUpdate Request() => new(SafeFacts() ?? new NoFieldLayoutFacts(), _services.Clock.GetLocalNow().DateTime);

    private IFieldLayoutFacts? SafeFacts()
    {
        try
        {
            return _services.Refresh.Facts(_editor);
        }
        catch (Exception exception) when (exception is InvalidOperationException or KeyNotFoundException)
        {
            return null;
        }
    }

    private string? ParagraphOf(ReferenceTarget target) => target.Kind switch
    {
        ReferenceTargetKind.Headings or ReferenceTargetKind.Captions => target.Key,
        ReferenceTargetKind.Bookmarks => _services.Bookmarks.Find(_editor.Document, target.Key)?.ParagraphId,
        _ => _services.Order.Paragraphs(_editor.Document).FirstOrDefault(paragraph => paragraph.Marks.OfType<NoteMark>().Any(mark => mark.NoteId == target.Key))?.Id,
    };

    private string Direction(string? paragraph)
    {
        if (paragraph is null) return Texts["folio.reference.above"];
        foreach (var candidate in _services.Order.Paragraphs(_editor.Document))
        {
            if (candidate.Id == paragraph) return Texts["folio.reference.above"];
            if (candidate.Id == _editor.Caret.ParagraphId) return Texts["folio.reference.below"];
        }
        return Texts["folio.reference.above"];
    }
}
