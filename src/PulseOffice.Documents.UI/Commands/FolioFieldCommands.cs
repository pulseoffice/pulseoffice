using System.Globalization;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Fields;
using PulseOffice.Localization;
using PulseOffice.Platform;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;

namespace PulseOffice.Documents.UI.Commands;

public sealed record FolioFieldChoice(string Key, TextFieldKind Kind, string? Argument = null)
{
    public string Category { get; init; } = FolioFieldCatalog.DocumentCategory;
}

public sealed record FolioFieldCategory(string Key, string Icon);

public sealed class FolioFieldCatalog
{
    public const string DocumentCategory = "document";
    public const string InfoCategory = "info";
    public const string FunctionsCategory = "functions";
    public const string VariablesCategory = "variables";
    public const string DatabaseCategory = "database";

    public IReadOnlyList<FolioFieldCategory> Categories { get; } =
    [
        new(DocumentCategory, "folio.field.category.document"),
        new(InfoCategory, "folio.field.category.info"),
        new(FunctionsCategory, "folio.field.category.functions"),
        new(VariablesCategory, "folio.field.category.variables"),
        new(DatabaseCategory, "folio.field.category.database"),
    ];

    public IReadOnlyList<FolioFieldChoice> Choices { get; } =
    [
        new("date", TextFieldKind.Date),
        new("time", TextFieldKind.Time),
        new("pageNumber", TextFieldKind.PageNumber),
        new("pageCount", TextFieldKind.PageCount),
        new("wordCount", TextFieldKind.WordCount),
        new("characterCount", TextFieldKind.CharacterCount),
        new("paragraphCount", TextFieldKind.ParagraphCount),
        new("tableCount", TextFieldKind.TableCount),
        new("imageCount", TextFieldKind.ImageCount),
        new("objectCount", TextFieldKind.ObjectCount),
        new("chapterName", TextFieldKind.Chapter),
        new("chapterNumber", TextFieldKind.Chapter, "number"),
        new("chapterNumberAndName", TextFieldKind.Chapter, "number-and-name"),
        new("fileName", TextFieldKind.FileName),
        new("title", TextFieldKind.Title) { Category = InfoCategory },
        new("subject", TextFieldKind.Subject) { Category = InfoCategory },
        new("author", TextFieldKind.Author) { Category = InfoCategory },
        new("keywords", TextFieldKind.Keywords) { Category = InfoCategory },
        new("description", TextFieldKind.Description) { Category = InfoCategory },
        new("createdDate", TextFieldKind.CreatedDate) { Category = InfoCategory },
        new("modifiedDate", TextFieldKind.ModifiedDate) { Category = InfoCategory },
        new("customProperty", TextFieldKind.CustomProperty) { Category = InfoCategory },
        new("conditionalText", TextFieldKind.ConditionalText) { Category = FunctionsCategory },
        new("hiddenText", TextFieldKind.HiddenText) { Category = FunctionsCategory },
        new("hiddenParagraph", TextFieldKind.HiddenParagraph) { Category = FunctionsCategory },
        new("variableSet", TextFieldKind.VariableSet) { Category = VariablesCategory },
        new("variableGet", TextFieldKind.Variable) { Category = VariablesCategory },
        new("userField", TextFieldKind.Variable, "user") { Category = VariablesCategory },
        new("database", TextFieldKind.Database) { Category = DatabaseCategory },
    ];

    public FolioFieldChoice? Of(TextRun run) =>
        Choices.FirstOrDefault(entry => entry.Kind == run.Field && entry.Argument == run.FieldArgument)
        ?? Choices.FirstOrDefault(entry => entry.Kind == run.Field && entry.Argument is null)
        ?? Choices.FirstOrDefault(entry => entry.Kind == run.Field);
}

public sealed class FolioFieldCommands(
    IActiveFolioContext context,
    IFolioForms forms,
    ITextCatalog texts,
    IFieldEditing fields,
    IReferenceTargets targets,
    IFolioFieldRefresh refresh,
    IFolioNavigation navigation,
    PulseOffice.UI.Controls.Links.ILinkFollower follower,
    FolioFieldCatalog catalog,
    ITabbedForms tabbed,
    IFolioLinks links,
    IFolioFieldsDialog dialog) : FolioCommandSet(context)
{
    private const string HeadingBookmark = "__RefHeading__";

    private readonly IReadOnlyList<string> _numberFormats = ["1", "a", "A", "i", "I"];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Editing("folio.insert.fieldDialog") with { DefaultGesture = "Ctrl+F2", Execute = (_, ct) => InsertFieldAsync(ct) },
        Editing("folio.edit.field") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && FieldAtCaret(editor) is not null,
            Execute = (_, ct) => EditFieldAsync(ct),
        },
        Action(Editing("folio.tools.updateFields") with { DefaultGesture = "F9" }, editor => refresh.UpdateFields(editor)),
        Action(Editing("folio.tools.updateField") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Selected(editor).Count > 0,
        }, editor => refresh.UpdateFields(editor, Selected(editor).Select(hit => hit.ParagraphId).ToHashSet(StringComparer.Ordinal))),
        Action(Editing("folio.tools.updateAll") with { DefaultGesture = "Ctrl+Shift+F9" }, editor => refresh.UpdateAll(editor)),
        Action(Editing("folio.tools.fieldToText") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && Selected(editor).Count > 0,
        }, editor => editor.Change(() => fields.ToText(editor.Document, editor.History, Selected(editor)))),
        Editing("folio.insert.crossReference") with { Execute = (_, ct) => CrossReferenceAsync(ct) },
        Editing("folio.insert.footnoteDialog") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => SpecialNoteAsync(ct) },
        Editing("folio.edit.footnote") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && NoteAtCaret(editor) is not null,
            Execute = (_, ct) => EditNoteAsync(ct),
        },
        Editing("folio.format.notes") with { Surface = CommandSurface.Advanced, Execute = (_, ct) => NoteSettingsAsync(ct) },
        Viewing("folio.hyperlink.open") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && editor.EffectiveRun().Hyperlink is not null,
            Execute = (_, _) => OpenLink(),
        },
        Action(Editing("folio.hyperlink.remove") with
        {
            Surface = CommandSurface.Advanced,
            EnabledWhen = _ => Editor is { } editor && !editor.ReadOnly() && editor.EffectiveRun().Hyperlink is not null,
        }, RemoveLink),
    ];

    private FieldHit? FieldAtCaret(FolioEditor editor)
    {
        try
        {
            return fields.At(editor.Document, editor.Caret);
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
    }

    private IReadOnlyList<FieldHit> Selected(FolioEditor editor)
    {
        if (!editor.HasSelection) return FieldAtCaret(editor) is { } hit ? [hit] : [];
        var range = editor.Selection;
        return fields.Within(editor.Document, editor.Services.Order.Between(editor.Document, range.Start, range.End), range);
    }

    private Task InsertFieldAsync(CancellationToken ct) =>
        Editor is { } editor ? dialog.ShowAsync(editor, new FolioFieldsStart(FolioFieldsPage.Fields), ct) : Task.CompletedTask;

    private Task CrossReferenceAsync(CancellationToken ct) =>
        Editor is { } editor ? dialog.ShowAsync(editor, new FolioFieldsStart(FolioFieldsPage.References), ct) : Task.CompletedTask;

    private Task EditFieldAsync(CancellationToken ct)
    {
        if (Editor is not { } editor || FieldAtCaret(editor) is not { } hit) return Task.CompletedTask;
        var run = hit.Run;
        if (run.Field is TextFieldKind.Bookmark or TextFieldKind.Reference or TextFieldKind.NoteReference)
        {
            var (kind, sequence, key) = ReferenceOf(editor, run);
            return dialog.ShowAsync(editor, new FolioFieldsStart(FolioFieldsPage.References)
            {
                Editing = hit,
                ReferenceKind = new FolioReferenceKind(kind, sequence, string.Empty, string.Empty),
                Target = new ReferenceTarget(kind, key, string.Empty),
                Format = run.FieldDetail?.Format ?? ReferenceFormats.Text,
            }, ct);
        }
        return dialog.ShowAsync(editor, new FolioFieldsStart(FolioFieldsPage.Fields)
        {
            Editing = hit,
            Choice = catalog.Of(run) ?? new FolioFieldChoice("other", run.Field),
        }, ct);
    }

    private (ReferenceTargetKind Kind, string? Sequence, string Key) ReferenceOf(FolioEditor editor, TextRun run)
    {
        var name = run.FieldArgument ?? string.Empty;
        if (run.Field == TextFieldKind.NoteReference)
            return (editor.Document.Notes.All.FirstOrDefault(note => note.Id == name)?.Kind == NoteKind.Endnote ? ReferenceTargetKind.Endnotes : ReferenceTargetKind.Footnotes, null, name);
        if (name.StartsWith(HeadingBookmark, StringComparison.Ordinal)) return (ReferenceTargetKind.Headings, null, name[HeadingBookmark.Length..]);
        foreach (var sequence in targets.Sequences(editor.Document))
            foreach (var caption in targets.Targets(editor.Document, ReferenceTargetKind.Captions, sequence))
                if (editor.Services.Stories.Find(editor.Document, caption.Key).Paragraph.Content.Runs.Any(candidate => candidate.Field == TextFieldKind.Sequence && candidate.FieldDetail?.Format == name))
                    return (ReferenceTargetKind.Captions, sequence, caption.Key);
        return (ReferenceTargetKind.Bookmarks, null, name);
    }

    private Note? NoteAtCaret(FolioEditor editor)
    {
        var caret = editor.Caret;
        foreach (var note in editor.Document.Notes.All)
            if (editor.Services.Stories.Paragraphs(editor.Document).Any(paragraph => paragraph.Id == caret.ParagraphId
                    && paragraph.Marks.OfType<Marks.NoteMark>().Any(mark => mark.NoteId == note.Id && (mark.Offset == caret.Offset || mark.Offset == caret.Offset - 1))))
                return note;
        return editor.Document.Notes.All.FirstOrDefault(note => editor.Services.Stories.Find(editor.Document, caret.ParagraphId).Story == note.Content);
    }

    private IReadOnlyList<FormField> NoteFields(NoteKind kind, string? citation) =>
    [
        new ChoiceField("kind", texts["folio.note.kind"], [texts["folio.note.kind.footnote"], texts["folio.note.kind.endnote"]], kind == NoteKind.Footnote ? 0 : 1),
        new CheckField("custom", texts["folio.note.custom"], citation is not null),
        new TextField("citation", texts["folio.note.citation"], citation ?? string.Empty, "*"),
    ];

    private async Task SpecialNoteAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var values = await forms.AskAsync("command.folio.insert.footnoteDialog.title", "folio.note.explanation", NoteFields(NoteKind.Footnote, null), ct).ConfigureAwait(true);
        if (values is null) return;
        var kind = values.Choice("kind", 2) == 0 ? NoteKind.Footnote : NoteKind.Endnote;
        var citation = values.Flag("custom") && values.Text("citation").Trim() is { Length: > 0 } custom ? custom : null;
        if (editor.Insert((documents, at) =>
            {
                documents.InsertNote(editor.Document, editor.History, at, kind, string.Empty);
                if (citation is not null && editor.Document.Notes.All[^1] is { } note) SetNote(editor, note, kind, citation);
            }, "insert-note"))
            editor.EnterNote(editor.Document.Notes.All[^1]);
    }

    private async Task EditNoteAsync(CancellationToken ct)
    {
        if (Editor is not { } editor || NoteAtCaret(editor) is not { } note) return;
        var values = await forms.AskAsync("command.folio.edit.footnote.title", "folio.note.explanation", NoteFields(note.Kind, note.Citation), ct).ConfigureAwait(true);
        if (values is null) return;
        var kind = values.Choice("kind", 2) == 0 ? NoteKind.Footnote : NoteKind.Endnote;
        var citation = values.Flag("custom") && values.Text("citation").Trim() is { Length: > 0 } custom ? custom : null;
        editor.Change(() => SetNote(editor, note, kind, citation));
    }

    private void SetNote(FolioEditor editor, Note note, NoteKind kind, string? citation)
    {
        var recorder = new EditRecorder(editor.Document, "edit-note").Touch<(NoteKind, string?)>(note, () => [(note.Kind, note.Citation)], state =>
        {
            note.Kind = state[0].Item1;
            note.Citation = state[0].Item2;
        });
        note.Kind = kind;
        note.Citation = citation;
        recorder.Commit(editor.History);
    }

    private async Task NoteSettingsAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var notes = editor.Document.Notes;
        FormField[] Tab(string prefix, NoteSettings settings, bool footnote) =>
        [
            new ChoiceField(prefix + "format", texts["folio.note.format"], [.. _numberFormats], Math.Max(0, _numberFormats.ToList().IndexOf(settings.FormatName))),
            new NumberField(prefix + "start", texts["folio.note.start"], settings.StartAt, 0, 9999) { Integer = true },
            new ChoiceField(prefix + "restart", texts["folio.note.restart"],
                footnote ? [texts["folio.note.restart.document"], texts["folio.note.restart.chapter"], texts["folio.note.restart.page"]] : [texts["folio.note.restart.document"], texts["folio.note.restart.chapter"]],
                (int)settings.Restart),
        ];
        var tabs = new List<FormTab>
        {
            new(texts["folio.note.kind.footnote"], texts["folio.note.settings.explanation"], Tab("foot.", notes.FootnoteSettings, true)),
            new(texts["folio.note.kind.endnote"], texts["folio.note.settings.explanation"], Tab("end.", notes.EndnoteSettings, false)),
        };
        if (await tabbed.AskAsync("command.folio.format.notes.title", tabs, ct, 0, 460).ConfigureAwait(true) is not { } values) return;
        NoteSettings Read(string prefix, NoteSettings settings, int restarts) => settings with
        {
            FormatName = _numberFormats[values.Choice(prefix + "format", _numberFormats.Count)],
            StartAt = (int)Math.Round(values.Number(prefix + "start", settings.StartAt)),
            Restart = (NoteNumbering)values.Choice(prefix + "restart", restarts),
        };
        var footnotes = Read("foot.", notes.FootnoteSettings, 3);
        var endnotes = Read("end.", notes.EndnoteSettings, 2);
        editor.Change(() =>
        {
            var recorder = new EditRecorder(editor.Document, "note-settings").Touch<NoteSettings>(notes, () => [notes.FootnoteSettings, notes.EndnoteSettings], state =>
            {
                notes.FootnoteSettings = state[0];
                notes.EndnoteSettings = state[1];
            });
            notes.FootnoteSettings = footnotes;
            notes.EndnoteSettings = endnotes;
            recorder.Commit(editor.History);
        });
    }

    private Task OpenLink()
    {
        if (Editor is not { } editor || editor.EffectiveRun().Hyperlink is not { } link) return Task.CompletedTask;
        follower.Follow(link, place => navigation.Follow(editor, place));
        return Task.CompletedTask;
    }

    private void RemoveLink(FolioEditor editor) =>
        editor.Apply((documents, target) => documents.MapRuns(editor.Document, editor.History, target, "remove-hyperlink", run => run with { Hyperlink = null }), links.Range(editor));
}
