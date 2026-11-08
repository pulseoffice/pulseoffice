using System.Globalization;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Localization;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls;
using PulseOffice.UI.Controls.Links;
using PulseOffice.UI.Controls.Objects;
using PulseOffice.UI.Controls.Services;
using PulseOffice.UI.Controls.Tables.Insert;

namespace PulseOffice.Documents.UI.Commands;

public sealed class FolioInsertCommands(
    IActiveFolioContext context,
    IFolioForms forms,
    IDialogService dialogs,
    IRasterImageFiles images,
    IFolioImages pictures,
    ITableInsertPrompt tables,
    IFolioTableLooks looks,
    ITextCatalog texts,
    IHyperlinkPrompt hyperlinks,
    IFolioLinks links,
    PulseOffice.Foundation.Identity.IUserIdentity identity) : FolioCommandSet(context)
{
    private readonly IReadOnlyList<(string Key, string Text, string? Gesture)> _characters =
    [
        ("nonBreakingSpace", "\u00A0", "Ctrl+Shift+Space"),
        ("narrowNonBreakingSpace", "\u202F", null),
        ("nonBreakingHyphen", "\u2011", null),
        ("softHyphen", "\u00AD", null),
        ("zeroWidthSpace", "\u200B", null),
        ("wordJoiner", "\u2060", null),
        ("leftToRightMark", "\u200E", null),
        ("rightToLeftMark", "\u200F", null),
    ];

    private readonly IReadOnlyList<(string Key, TextFieldKind Kind)> _fields =
    [
        ("pageNumber", TextFieldKind.PageNumber),
        ("pageCount", TextFieldKind.PageCount),
        ("date", TextFieldKind.Date),
        ("time", TextFieldKind.Time),
        ("title", TextFieldKind.Title),
        ("author", TextFieldKind.Author),
        ("fileName", TextFieldKind.FileName),
    ];

    public IEnumerable<CommandDefinition> GetCommands() =>
    [
        Action(Editing("folio.insert.pageBreak") with { DefaultGesture = "Ctrl+Enter" }, editor => Break(editor, new Styles.ParagraphProperties { PageBreakBefore = true }, "page-break")),
        Action(Editing("folio.insert.columnBreak") with { DefaultGesture = "Ctrl+Shift+Enter" }, editor => Break(editor, new Styles.ParagraphProperties { ColumnBreakBefore = true }, "column-break")),
        Editing("folio.insert.table") with
        {
            RibbonControl = new TableGridSpec(Limits.MaxRows, Limits.MaxColumns),
            Execute = (command, ct) => TableAsync(command.Argument as RibbonTableSize, ct),
        },
        Editing("folio.insert.image") with { Execute = (_, ct) => ImageAsync(ct) },
        Editing("folio.insert.symbol") with { Execute = (_, ct) => SymbolAsync(ct) },
        .. _characters.Select(Character),
        .. _fields.Select(Field),
        Action(Editing("folio.insert.footnote"), editor => Note(editor, NoteKind.Footnote)),
        Action(Editing("folio.insert.endnote"), editor => Note(editor, NoteKind.Endnote)),
        Editing("folio.insert.bookmark") with { Execute = (_, ct) => BookmarkAsync(ct) },
        Editing("folio.insert.comment") with { Execute = (_, ct) => CommentAsync(ct) },
        Editing("folio.insert.hyperlink") with { DefaultGesture = "Ctrl+K", Execute = (_, ct) => HyperlinkAsync(ct) },
    ];

    private CommandDefinition Character((string Key, string Text, string? Gesture) character) =>
        Action(Editing($"folio.insert.character.{character.Key}") with
        {
            Surface = CommandSurface.Advanced,
            DefaultGesture = character.Gesture,
        }, editor => editor.Type(character.Text));

    private CommandDefinition Field((string Key, TextFieldKind Kind) field) =>
        Action(Editing($"folio.insert.field.{field.Key}") with { Surface = CommandSurface.Advanced }, editor =>
            editor.Insert((documents, at) => documents.InsertField(editor.Document, editor.History, at, field.Kind, Display(editor, field.Kind)), "insert-field"));

    private string Display(FolioEditor editor, TextFieldKind kind) => kind switch
    {
        TextFieldKind.Date => DateTime.Now.ToString("d", editor.Culture()),
        TextFieldKind.Time => DateTime.Now.ToString("t", editor.Culture()),
        TextFieldKind.Title => editor.Document.Properties.Title is { Length: > 0 } title ? title : texts["folio.field.untitled"],
        TextFieldKind.Author => editor.Document.Properties.Author is { Length: > 0 } author ? author : identity.Name,
        TextFieldKind.FileName => editor.Document.FileName is { Length: > 0 } name ? name : texts["folio.field.untitled"],
        _ => "1",
    };

    private void Break(FolioEditor editor, Styles.ParagraphProperties change, string label) =>
        editor.Insert((documents, at) =>
        {
            var split = documents.SplitParagraph(editor.Document, editor.History, at);
            var created = ((ParagraphSplit)split.Changes[0]).NewParagraphId;
            documents.SetParagraph(editor.Document, editor.History, DocumentRange.In(created, 0, 0), change);
            editor.PlaceCaret(new DocumentPosition(created, 0), false);
        }, label);

    private void Note(FolioEditor editor, NoteKind kind)
    {
        if (editor.Insert((documents, at) => documents.InsertNote(editor.Document, editor.History, at, kind, string.Empty), "insert-note"))
            editor.EnterNote(editor.Document.Notes.All[^1]);
    }

    private TableInsertLimits Limits { get; } = new(500, 64);

    private async Task TableAsync(RibbonTableSize? quick, CancellationToken ct)
    {
        if (Editor is null) return;
        var choice = quick is { } size
            ? tables.Quick(size.Rows, size.Columns, Limits)
            : await tables.AskAsync(new TableInsertRequest("command.folio.insert.table.title") { Limits = Limits }, ct).ConfigureAwait(true);
        if (choice is null || Editor is not { } editor) return;
        var container = editor.Services.Stories.Find(editor.Document, editor.Caret.ParagraphId).Location.Container;
        var existing = container.Blocks.OfType<PulseOffice.Documents.Blocks.TableBlock>().Select(table => table.Id).ToHashSet(StringComparer.Ordinal);
        using (editor.History.BeginTransaction("insert-table"))
        {
            editor.Insert((documents, at) => documents.InsertTable(editor.Document, editor.History, at, choice.Rows, choice.Columns), "insert-table");
            if (container.Blocks.OfType<PulseOffice.Documents.Blocks.TableBlock>().FirstOrDefault(table => !existing.Contains(table.Id)) is not { } created) return;
            looks.Apply(editor, created, choice.Look, choice.HeaderRow, choice.BandedRows);
            if (created.Cell(0, 0).Content.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().FirstOrDefault() is { } first)
                editor.Select(new PulseOffice.Documents.Editing.DocumentPosition(first.Id, 0), new PulseOffice.Documents.Editing.DocumentPosition(first.Id, 0));
        }
    }

    private async Task ImageAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        if (await images.PickAsync(ct).ConfigureAwait(true) is not { } source) return;
        pictures.Insert(editor, source.Data, source.MediaType, source.FileName);
    }

    private async Task SymbolAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var glyph = await dialogs.ShowAsync<string>(
            new SymbolPickerView(texts),
            new DialogOptions { TitleKey = "command.folio.insert.symbol.title", PreferredWidth = 900 },
            ct).ConfigureAwait(true);
        if (!string.IsNullOrEmpty(glyph)) editor.Type(glyph);
    }

    private async Task BookmarkAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        FormField[] fields = [new TextField("name", texts["folio.bookmark.name"], texts["folio.bookmark.default"])];
        if (await forms.AskAsync("command.folio.insert.bookmark.title", "folio.bookmark.explanation", fields, ct).ConfigureAwait(true) is not { } values) return;
        if (values.Text("name").Trim() is not { Length: > 0 } name) return;
        editor.Apply((documents, range) => documents.AddBookmark(editor.Document, editor.History, range, name), editor.Selection);
    }

    private async Task CommentAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        FormField[] fields = [new TextField("text", texts["folio.comment.text"]) { Multiline = true }];
        if (await forms.AskAsync("command.folio.insert.comment.title", "folio.comment.explanation", fields, ct).ConfigureAwait(true) is not { } values) return;
        if (values.Text("text").Trim() is not { Length: > 0 } text) return;
        var author = editor.Document.Properties.Author is { Length: > 0 } known ? known : identity.Name;
        editor.Apply((documents, range) => documents.AddComment(editor.Document, editor.History, range, author, DateTimeOffset.Now, text), editor.CharacterRange());
    }

    private async Task HyperlinkAsync(CancellationToken ct)
    {
        if (Editor is not { } editor) return;
        var current = editor.EffectiveRun().Hyperlink;
        var request = new LinkRequest("command.folio.insert.hyperlink.title")
        {
            Address = current ?? string.Empty,
            Text = editor.HasSelection ? editor.SelectedText().ReplaceLineEndings(" ") : null,
            TextEditable = !editor.HasSelection,
            Places = links.Places(editor.Document),
            CanRemove = current is not null,
        };
        if (await hyperlinks.AskAsync(request, ct).ConfigureAwait(true) is not { } choice || Editor is not { } target) return;
        if (choice.Remove)
        {
            target.Apply((documents, range) => documents.MapRuns(target.Document, target.History, range, "remove-hyperlink", run => run with { Hyperlink = null }), links.Range(target));
            return;
        }
        if (target.HasSelection || current is not null)
        {
            target.Apply((documents, range) => documents.MapRuns(target.Document, target.History, range, "hyperlink", run => run with { Hyperlink = choice.Address }), links.Range(target));
            return;
        }
        var shown = choice.Text.ReplaceLineEndings(" ");
        target.Insert((documents, at) =>
        {
            documents.InsertText(target.Document, target.History, at, shown);
            documents.MapRuns(target.Document, target.History, DocumentRange.In(at.ParagraphId, at.Offset, at.Offset + shown.Length), "hyperlink",
                run => run with { Hyperlink = choice.Address });
        }, "hyperlink");
    }
}
