using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Indexes;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioIndexCommands(
    IActiveFolioContext context,
    IFolioForms forms,
    ITabbedForms tabbed,
    ITextCatalog texts,
    IIndexEditing indexes,
    IFolioFieldRefresh refresh,
    IReferenceTargets targets,
    BibliographyFieldNames bibliography,
    ISidePanelHost panel,
    PulseOffice.Documents.UI.Rendering.IFolioViewMarks marks,
    PulseOffice.Documents.UI.Indexes.IFolioIndexGallery gallery,
    PulseOffice.Documents.UI.Indexes.IFolioIndexEntries entries,
    PulseOffice.UI.Controls.Services.IStatusBarService status) : FolioCommandSet(context)
{
    public const string NavigatorPage = "navigator";

    private readonly IReadOnlyList<CaptionDisplay> _captions = Enum.GetValues<CaptionDisplay>();

    private readonly IReadOnlyList<(IndexObjectSources Source, string Key)> _objectSources =
    [
        (IndexObjectSources.Charts, "charts"), (IndexObjectSources.Formulas, "formulas"), (IndexObjectSources.Drawings, "drawings"),
        (IndexObjectSources.Spreadsheets, "spreadsheets"), (IndexObjectSources.Other, "other"),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Action(Editing("folio.insert.tableOfContents"), editor => Insert(editor, Defaults(IndexKind.Contents, editor))),
        Editing("folio.insert.index") with { Execute = (_, ct) => InsertIndexAsync(ct) },
        Editing("folio.index.edit") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Around(editor) is not null,
            Execute = (_, ct) => EditIndexAsync(ct),
        },
        Action(Editing("folio.index.update") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Around(editor) is not null,
        }, editor => refresh.UpdateIndexes(editor, Around(editor)!.Id)),
        Action(Editing("folio.tools.updateIndexes") with
        {
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && indexes.Indexes(editor.Document).Count > 0,
        }, editor => refresh.UpdateIndexes(editor)),
        Action(Editing("folio.index.remove") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Around(editor) is not null,
        }, editor =>
        {
            var section = Around(editor)!;
            var after = NextParagraph(editor, section);
            editor.Change(() => indexes.Remove(editor.Document, editor.History, section.Id));
            if (after is not null) editor.PlaceCaret(new DocumentPosition(after, 0), false);
        }),
        Editing("folio.insert.indexEntry") with { Execute = (_, _) => IndexEntryAsync() },
        Editing("folio.index.editEntry") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && EntryAt(editor) is not null,
            Execute = (_, ct) => EditEntryAsync(ct),
        },
        Editing("folio.insert.bibliographyEntry") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => CitationAsync(ct) },
        Editing("folio.edit.citation") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && editor.EffectiveRun().Field == TextFieldKind.Citation,
            Execute = (_, ct) => EditCitationAsync(ct),
        },
        Editing("folio.tools.bibliography") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => DatabaseAsync(ct) },
        Viewing("folio.view.fieldShadings") with
        {
            DefaultGesture = "Ctrl+F8",
            CheckedWhen = _ => marks.FieldShadings,
            Execute = (_, _) =>
            {
                marks.FieldShadings = !marks.FieldShadings;
                return Task.CompletedTask;
            },
        },
        Viewing("folio.view.navigator") with
        {
            DefaultGesture = "F5",
            Execute = (_, _) =>
            {
                panel.Show(NavigatorPage);
                return Task.CompletedTask;
            },
        },
    ];

    private SectionBlock? Around(FolioEditor editor)
    {
        try
        {
            return indexes.Around(editor.Document, editor.Caret.ParagraphId);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private string? NextParagraph(FolioEditor editor, SectionBlock section)
    {
        var body = editor.Document.Body;
        var index = body.IndexOf(section);
        return index >= 0 && index + 1 < body.Count && body[index + 1] is ParagraphBlock next ? next.Id
            : index > 0 && body[index - 1] is ParagraphBlock previous ? previous.Id : null;
    }

    private string Kind(IndexKind kind) => texts["folio.index.kind." + kind.ToString().ToLowerInvariant()];

    private IndexDefinition Defaults(IndexKind kind, FolioEditor editor) => new(kind)
    {
        Title = Kind(kind),
        Levels = kind == IndexKind.Contents ? 10 : 1,
        Sequence = kind switch
        {
            IndexKind.Illustrations => targets.Sequences(editor.Document).FirstOrDefault(sequence => !IsTable(sequence)) ?? "Figure",
            IndexKind.Tables => targets.Sequences(editor.Document).FirstOrDefault(IsTable) ?? "Table",
            _ => null,
        },
        Hyperlinks = kind == IndexKind.Contents,
        PageNumbers = kind != IndexKind.Bibliography,
        UserName = kind == IndexKind.User ? "User-Defined" : null,
        SortByPosition = kind != IndexKind.Bibliography,
    };

    private bool IsTable(string sequence) => sequence is "Table" or "Tabella" or "Tabelle" or "Tabla" or "Tableau";

    private void Insert(FolioEditor editor, IndexDefinition definition)
    {
        if (editor.RefusesEdit()) return;
        var at = editor.Caret;
        using (editor.History.BeginTransaction("insert-index"))
        {
            editor.Change(() => indexes.Insert(editor.Document, editor.History, at, definition, refresh.Facts(editor)));
            refresh.UpdateIndexes(editor);
        }
    }

    private async Task InsertIndexAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        if (await gallery.AskAsync(ct).ConfigureAwait(true) is not { } pick || Editor is not { } target) return;
        var chosen = pick.Preset.Apply(Defaults(pick.Preset.Kind, target));
        if (pick.Customize)
        {
            if (await SettingsAsync(target, chosen, "command.folio.insert.index.title", ct).ConfigureAwait(true) is not { } custom) return;
            chosen = custom;
        }
        Insert(target, chosen);
    }

    private async Task EditIndexAsync(CancellationToken ct)
    {
        if (Editor is not { } editor || Around(editor) is not { Index: { } current } section) return;
        if (await SettingsAsync(editor, current, "command.folio.index.edit.title", ct).ConfigureAwait(true) is not { } definition) return;
        using (editor.History.BeginTransaction("change-index"))
        {
            editor.Change(() => indexes.Change(editor.Document, editor.History, section.Id, definition, refresh.Facts(editor)));
            refresh.UpdateIndexes(editor, section.Id);
        }
    }

    private async Task<IndexDefinition?> SettingsAsync(FolioEditor editor, IndexDefinition current, string titleKey, CancellationToken ct)
    {
        var general = new List<FormField>
        {
            new TextField("title", texts["folio.index.title"], current.Title),
            new CheckField("pages", texts["folio.index.pages"], current.PageNumbers),
            new CheckField("links", texts["folio.index.links"], current.Hyperlinks),
        };
        var specific = new List<FormField>();
        var sequences = targets.Sequences(editor.Document).ToList();
        switch (current.Kind)
        {
            case IndexKind.Contents:
                specific.Add(new NumberField("levels", texts["folio.index.levels"], current.Levels, 1, 10) { Integer = true });
                specific.Add(new CheckField("outline", texts["folio.index.outline"], current.FromOutline));
                specific.Add(new CheckField("marks", texts["folio.index.marks"], current.FromMarks));
                break;
            case IndexKind.Alphabetical:
                specific.Add(new CheckField("letters", texts["folio.index.letters"], current.LetterSeparators));
                specific.Add(new CheckField("combine", texts["folio.index.combine"], current.CombineEntries));
                specific.Add(new CheckField("case", texts["folio.index.case"], current.CaseSensitive));
                break;
            case IndexKind.Illustrations or IndexKind.Tables:
                if (current.Sequence is { } sequence && !sequences.Contains(sequence)) sequences.Insert(0, sequence);
                if (sequences.Count == 0) sequences.Add(current.Kind == IndexKind.Tables ? "Table" : "Figure");
                specific.Add(new ChoiceField("sequence", texts["folio.index.sequence"], sequences, Math.Max(0, sequences.IndexOf(current.Sequence ?? sequences[0]))));
                specific.Add(new ChoiceField("display", texts["folio.index.display"], [.. _captions.Select(display => texts["folio.index.display." + display.ToString().ToLowerInvariant()])], _captions.ToList().IndexOf(current.Captions)));
                break;
            case IndexKind.Bibliography:
                specific.Add(new CheckField("order", texts["folio.index.byPosition"], current.SortByPosition));
                break;
            case IndexKind.Objects:
                specific.AddRange(_objectSources.Select(entry => (FormField)new CheckField("objects." + entry.Key, texts["folio.index.objects." + entry.Key], current.ObjectSources.HasFlag(entry.Source))));
                break;
            case IndexKind.User:
                specific.Add(new TextField("name", texts["folio.index.userName"], current.UserName ?? "User-Defined"));
                specific.Add(new CheckField("order", texts["folio.index.byPosition"], current.SortByPosition));
                break;
        }
        var tabs = new List<FormTab> { new(texts["folio.index.tab.general"], texts["folio.index.general.explanation"], general) };
        if (specific.Count > 0) tabs.Add(new(Kind(current.Kind), texts["folio.index.specific.explanation"], specific));
        if (await tabbed.AskAsync(titleKey, tabs, ct, 0, 520).ConfigureAwait(true) is not { } values) return null;
        return current with
        {
            Title = values.Text("title").Trim(),
            PageNumbers = values.Flag("pages"),
            Hyperlinks = values.Flag("links"),
            Levels = current.Kind == IndexKind.Contents ? (int)Math.Round(values.Number("levels", current.Levels)) : current.Levels,
            FromOutline = current.Kind != IndexKind.Contents || values.Flag("outline"),
            FromMarks = current.Kind != IndexKind.Contents ? current.FromMarks : values.Flag("marks"),
            LetterSeparators = current.Kind == IndexKind.Alphabetical ? values.Flag("letters") : current.LetterSeparators,
            CombineEntries = current.Kind != IndexKind.Alphabetical || values.Flag("combine"),
            CaseSensitive = current.Kind == IndexKind.Alphabetical && values.Flag("case"),
            Sequence = current.Kind is IndexKind.Illustrations or IndexKind.Tables ? sequences[values.Choice("sequence", sequences.Count)] : current.Sequence,
            Captions = current.Kind is IndexKind.Illustrations or IndexKind.Tables ? _captions[values.Choice("display", _captions.Count)] : current.Captions,
            SortByPosition = current.Kind is IndexKind.Bibliography or IndexKind.User ? values.Flag("order") : current.SortByPosition,
            UserName = current.Kind == IndexKind.User && values.Text("name").Trim() is { Length: > 0 } name ? name : current.UserName,
            ObjectSources = current.Kind == IndexKind.Objects
                ? _objectSources.Where(entry => values.Flag("objects." + entry.Key)).Aggregate(IndexObjectSources.None, (all, entry) => all | entry.Source)
                : current.ObjectSources,
        };
    }

    public const string IndexEntryPage = "indexEntry";

    private Task IndexEntryAsync()
    {
        if (!panel.Pages.Contains(IndexEntryPage))
            panel.Register(new SidePanelPage(IndexEntryPage, () => new PulseOffice.Documents.UI.Indexes.FolioIndexEntryPanel(Context, entries, texts)));
        panel.Show(IndexEntryPage);
        return Task.CompletedTask;
    }

    private IndexEntryMark? EntryAt(FolioEditor editor)
    {
        var caret = editor.Caret;
        var paragraph = editor.Services.Stories.Find(editor.Document, caret.ParagraphId).Paragraph;
        return paragraph.Marks.OfType<IndexEntryMark>()
            .Where(mark => Math.Abs(mark.Offset - caret.Offset) <= 1 || editor.HasSelection && mark.Offset >= editor.Selection.Start.Offset && mark.Offset <= editor.Selection.End.Offset)
            .MinBy(mark => Math.Abs(mark.Offset - caret.Offset));
    }

    private async Task EditEntryAsync(CancellationToken ct)
    {
        if (Editor is not { } editor || EntryAt(editor) is not { } mark) return;
        var split = mark.Entry.IndexOf(", ", StringComparison.Ordinal);
        var (key, entry) = split > 0 ? (mark.Entry[..split], mark.Entry[(split + 2)..]) : (string.Empty, mark.Entry);
        var values = await forms.AskAsync("command.folio.index.editEntry.title", "folio.indexEntry.edit.explanation",
        [
            new TextField("entry", texts["folio.indexEntry.entry"], entry),
            new TextField("key", texts["folio.indexEntry.key"], key),
            new CheckField("delete", texts["folio.indexEntry.delete"]),
        ], ct).ConfigureAwait(true);
        if (values is null || Editor is not { } current) return;
        var text = values.Text("entry").Trim();
        var newKey = values.Text("key").Trim();
        var paragraph = current.Services.Stories.Find(current.Document, current.Caret.ParagraphId).Paragraph;
        current.Change(() =>
        {
            var recorder = new EditRecorder(current.Document, "index-entry-edit").Touch(paragraph);
            var marks = paragraph.Marks.Where(existing => !ReferenceEquals(existing, mark)).ToList();
            if (!values.Flag("delete") && text.Length > 0) marks.Add(mark with { Entry = newKey.Length > 0 ? newKey + ", " + text : text });
            paragraph.State = paragraph.State with { Marks = marks };
            recorder.Commit(current.History);
        });
    }

    private async Task CitationAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var sources = editor.Document.Bibliography.All.ToList();
        var labels = sources.Select(source => source.Identifier + " — " + source.Value("title")).Append(texts["folio.bibliography.new"]).ToList();
        var values = await forms.AskAsync("command.folio.insert.bibliographyEntry.title", "folio.bibliography.cite.explanation",
            [new ChoiceField("source", texts["folio.bibliography.source"], labels)], ct, 480).ConfigureAwait(true);
        if (values is null) return;
        var choice = values.Choice("source", labels.Count);
        var source = choice < sources.Count ? sources[choice] : await AskSourceAsync(null, ct).ConfigureAwait(true);
        if (source is null) return;
        using (editor.History.BeginTransaction("insert-citation"))
        {
            if (choice >= sources.Count && !Save(editor, null, source)) return;
            editor.Insert((documents, position) =>
                documents.InsertField(editor.Document, editor.History, position, TextFieldKind.Citation, "[" + source.Identifier + "]", source.Identifier), "insert-citation");
        }
    }

    private async Task EditCitationAsync(CancellationToken ct)
    {
        if (Editor is not { } editor || editor.EffectiveRun() is not { Field: TextFieldKind.Citation, FieldArgument: { } identifier }) return;
        var source = editor.Document.Bibliography.All.FirstOrDefault(candidate => candidate.Identifier == identifier);
        if (await AskSourceAsync(source, ct).ConfigureAwait(true) is { } edited) Save(editor, source, edited);
    }

    private async Task DatabaseAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        while (true)
        {
            var sources = editor.Document.Bibliography.All.ToList();
            var labels = sources.Select(source => source.Identifier + " — " + source.Value("author") + ", " + source.Value("title")).Append(texts["folio.bibliography.new"]).ToList();
            var values = await forms.AskAsync("command.folio.tools.bibliography.title", "folio.bibliography.explanation",
            [
                new ChoiceField("source", texts["folio.bibliography.source"], labels),
                new CheckField("delete", texts["folio.bibliography.delete"]),
            ], ct, 520).ConfigureAwait(true);
            if (values is null) return;
            var choice = values.Choice("source", labels.Count);
            if (choice < sources.Count && values.Flag("delete"))
            {
                Store(editor, sources[choice].Identifier, null);
                continue;
            }
            var current = choice < sources.Count ? sources[choice] : null;
            if (await AskSourceAsync(current, ct).ConfigureAwait(true) is not { } edited) return;
            Save(editor, current, edited);
        }
    }

    private async Task<BibliographySource?> AskSourceAsync(BibliographySource? current, CancellationToken ct)
    {
        var types = bibliography.Types;
        var main = new List<FormField>
        {
            new TextField("identifier", texts["folio.bibliography.identifier"], current?.Identifier ?? string.Empty),
            new ChoiceField("type", texts["folio.bibliography.type"], [.. types.Select(type => texts["folio.bibliography.type." + type])], Math.Max(0, types.ToList().IndexOf(current?.Type ?? "book"))),
        };
        main.AddRange(new[] { "author", "title", "year", "publisher", "address", "url" }.Select(field => new TextField(field, texts["folio.bibliography.field." + field], current?.Value(field) ?? string.Empty)));
        var rest = bibliography.All.Where(field => field is not ("author" or "title" or "year" or "publisher" or "address" or "url"))
            .Select(field => (FormField)new TextField(field, texts["folio.bibliography.field." + field.Replace("-", string.Empty, StringComparison.Ordinal)], current?.Value(field) ?? string.Empty)).ToList();
        var values = await tabbed.AskAsync("command.folio.tools.bibliography.title",
        [
            new FormTab(texts["folio.bibliography.tab.main"], texts["folio.bibliography.edit.explanation"], main),
            new FormTab(texts["folio.bibliography.tab.more"], texts["folio.bibliography.edit.explanation"], rest),
        ], ct, 0, 560).ConfigureAwait(true);
        if (values is null || values.Text("identifier").Trim() is not { Length: > 0 } identifier) return null;
        var fields = bibliography.All.Select(field => (field, value: values.Text(field).Trim())).Where(pair => pair.value.Length > 0)
            .ToDictionary(pair => pair.field, pair => pair.value, StringComparer.Ordinal);
        return new BibliographySource(identifier, types[values.Choice("type", types.Count)], fields);
    }

    private bool Save(FolioEditor editor, BibliographySource? current, BibliographySource source)
    {
        var renamed = current is not null && current.Identifier != source.Identifier;
        if ((current is null || renamed) && editor.Document.Bibliography.All.Any(other => other.Identifier == source.Identifier))
        {
            status.Report(texts.Format("folio.bibliography.taken", source.Identifier), PulseOffice.UI.Controls.Notifications.NotificationSeverity.Warning);
            return false;
        }
        using var transaction = editor.History.BeginTransaction("bibliography");
        if (renamed) Store(editor, current!.Identifier, null);
        Store(editor, source.Identifier, source);
        if (renamed) Retarget(editor, current!.Identifier, source.Identifier);
        return true;
    }

    private void Retarget(FolioEditor editor, string from, string to)
    {
        var document = editor.Document;
        var citing = editor.Services.Stories.Paragraphs(document)
            .Where(paragraph => paragraph.Content.Runs.Any(run => Cites(run, from)))
            .ToList();
        if (citing.Count == 0) return;
        editor.Change(() =>
        {
            foreach (var paragraph in citing)
                editor.Services.Editor.MapRuns(document, editor.History, DocumentRange.In(paragraph.Id, 0, paragraph.Length), "bibliography",
                    run => Cites(run, from) ? run with { FieldArgument = to } : run);
        });
        refresh.UpdateFields(editor, citing.Select(paragraph => paragraph.Id).ToHashSet(StringComparer.Ordinal));
    }

    private bool Cites(TextRun run, string identifier) => run.Field == TextFieldKind.Citation && run.FieldArgument == identifier;

    private void Store(FolioEditor editor, string identifier, BibliographySource? source)
    {
        var library = editor.Document.Bibliography;
        editor.Change(() =>
        {
            var recorder = new EditRecorder(editor.Document, "bibliography").Touch<BibliographySource>(library, () => library.All, library.Replace);
            if (source is null) library.Remove(identifier);
            else library.Store(source);
            recorder.Commit(editor.History);
        });
    }
}
