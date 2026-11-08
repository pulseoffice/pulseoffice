using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Protection;
using PulseOffice.Documents.Revisions;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Foundation.Modules;
using PulseOffice.Scripting;
using PulseOffice.Scripting.Editor;
using PulseOffice.Scripting.Editor.Facades;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Scripting;

public sealed class FolioScriptHost(PulseOffice.Exchange.IObjectExchange exchange, IScriptClipboardOperations operations) : IScriptHost
{
    public string ModuleName => "Folio";

    public bool HasClipboard => exchange.CanPaste(ModuleKind.Documents);

    public Task Copy() => operations.RunAsync("command.folio.clipboard.copy.title", () => exchange.CopyAsync(ModuleKind.Documents));

    public Task Cut() => operations.RunAsync("command.folio.clipboard.cut.title", () => exchange.CutAsync(ModuleKind.Documents));

    public Task Paste() => operations.RunAsync("command.folio.clipboard.paste.title", () => exchange.PasteAsync(ModuleKind.Documents));

    public Task PasteValues() => operations.RunAsync("command.folio.clipboard.paste.title", () => exchange.PasteAsync(ModuleKind.Documents, PulseOffice.Exchange.ExchangePasteMode.UnformattedText));
}

public sealed record FolioScriptServices(
    IBlockTree Tree,
    IDocumentSearch Search,
    IRevisionResolver Revisions,
    IFolioFieldRefresh Fields,
    IDocumentStatisticsCounter Statistics);

public sealed class FolioFacade(IScriptDispatcher dispatcher, IActiveFolioContext context, FolioScriptServices services)
    : ScriptFacade(dispatcher)
{
    private const int MaximumTableRows = 1000;

    private const int MaximumTableColumns = 64;

    private readonly IReadOnlyDictionary<string, TextFieldKind> FieldNames =
        new Dictionary<string, TextFieldKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["date"] = TextFieldKind.Date,
            ["time"] = TextFieldKind.Time,
            ["page"] = TextFieldKind.PageNumber,
            ["pages"] = TextFieldKind.PageCount,
            ["author"] = TextFieldKind.Author,
            ["title"] = TextFieldKind.Title,
            ["subject"] = TextFieldKind.Subject,
            ["filename"] = TextFieldKind.FileName,
            ["words"] = TextFieldKind.WordCount,
            ["characters"] = TextFieldKind.CharacterCount,
            ["paragraphs"] = TextFieldKind.ParagraphCount,
        };

    public string Name => Read(() => Editor()?.Document.FileName ?? string.Empty);

    public string Text => Read(() => string.Join('\n', Paragraphs().Select(paragraph => paragraph.Text.Replace('\u2028', '\n'))));

    public int ParagraphCount => Read(() => Paragraphs().Count);

    public int WordCount => Read(() => services.Statistics.Count(Paragraphs().Select(paragraph => paragraph.Text)).Words);

    public int TableCount => Read(() => Tables().Count);

    public int MacroCount => Read(() => Editor()?.Document.Macros.All.Count ?? 0);

    public string SelectedText => Read(() => Editor()?.SelectedText() ?? string.Empty);

    public bool TrackChanges => Read(() => Editor()?.Document.Changes.IsRecording == true);

    public int ChangeCount => Read(() => Editor()?.Document.Changes.All.Count ?? 0);

    public string Paragraph(double number) => Read(() => Existing(number).Text.Replace('\u2028', '\n'));

    public bool SetParagraph(double number, string text)
    {
        Change(() =>
        {
            var editor = Writable();
            var paragraph = Existing(number);
            Unguarded(editor, paragraph, $"Paragraph {Number(number)}");
            if (!editor.Replace(DocumentRange.In(paragraph.Id, 0, paragraph.Length), text ?? string.Empty))
                throw new InvalidOperationException($"Paragraph {Number(number)} contains protected content (a content control or form field) and cannot be replaced.");
        });
        return true;
    }

    public int AppendParagraph(string text)
    {
        var added = 0;
        Change(() =>
        {
            var editor = Writable();
            var all = Paragraphs();
            if (all.Count == 0) throw new InvalidOperationException("The document body has no paragraph to append after.");
            var last = all[^1];
            Unguarded(editor, last, $"The last paragraph ({all.Count})");
            editor.Select(new DocumentPosition(last.Id, last.Length), new DocumentPosition(last.Id, last.Length));
            var blankDocument = all.Count == 1 && last.Length == 0;
            if (!blankDocument && !editor.SplitParagraph())
                throw new InvalidOperationException($"A paragraph cannot be added after paragraph {all.Count}.");
            if (!string.IsNullOrEmpty(text)) editor.Type(text);
            added = Paragraphs().Count;
        });
        return added;
    }

    public bool InsertText(string text)
    {
        if (string.IsNullOrEmpty(text)) throw new ArgumentException("The text to insert is empty.", nameof(text));
        Change(() =>
        {
            if (!AtCursor().Type(text)) throw new InvalidOperationException("The text cannot be typed at the cursor.");
        });
        return true;
    }

    public string Style(double number) => Read(() => Existing(number).StyleName ?? string.Empty);

    public bool SetStyle(double number, string style)
    {
        if (string.IsNullOrWhiteSpace(style)) throw new ArgumentException("A paragraph style name is required: Styles() lists them.", nameof(style));
        Change(() =>
        {
            var editor = Writable();
            var paragraph = Existing(number);
            if (!editor.Document.Styles.Paragraph.Contains(style))
                throw new ArgumentException($"There is no paragraph style '{style}'. The document has: {string.Join(", ", Styles())}.", nameof(style));
            Unguarded(editor, paragraph, $"Paragraph {Number(number)}");
            editor.Change(() => editor.Services.Editor.ApplyParagraphStyle(editor.Document, editor.History, DocumentRange.In(paragraph.Id, 0, paragraph.Length), style));
        });
        return true;
    }

    public IReadOnlyList<string> Styles() =>
        Read(() => (IReadOnlyList<string>)(Editor()?.Document.Styles.Paragraph.All.Select(style => style.Name).ToList() ?? []));

    public bool Bold(double number, bool on) => Character(number, new() { Bold = on });

    public bool Italic(double number, bool on) => Character(number, new() { Italic = on });

    public bool SelectParagraph(double number)
    {
        Show(() =>
        {
            var editor = Open();
            var paragraph = Existing(number);
            editor.Select(new DocumentPosition(paragraph.Id, 0), new DocumentPosition(paragraph.Id, paragraph.Length));
        });
        return true;
    }

    public bool Select(double number, double start, double end)
    {
        Show(() =>
        {
            var editor = Open();
            var paragraph = Existing(number);
            var from = Offset(start, nameof(start), 0, paragraph, number);
            var to = Offset(end, nameof(end), from, paragraph, number);
            editor.Select(new DocumentPosition(paragraph.Id, from), new DocumentPosition(paragraph.Id, to));
        });
        return true;
    }

    public int Find(string text)
    {
        if (string.IsNullOrEmpty(text)) throw new ArgumentException("The text to find is empty.", nameof(text));
        return Read(() => services.Search.FindAll(Paragraphs(), new SearchQuery(text)).Count);
    }

    public int Replace(string text, string replacement)
    {
        if (string.IsNullOrEmpty(text)) throw new ArgumentException("The text to replace is empty.", nameof(text));
        var count = 0;
        Change(() =>
        {
            var editor = Writable();
            var found = services.Search.FindAll(Paragraphs(), new SearchQuery(text, MatchCase: true))
                .Where(range => Verdict(editor, range.Start.ParagraphId) == EditVerdict.Allowed)
                .ToList();
            var value = replacement ?? string.Empty;
            foreach (var range in Enumerable.Reverse(found))
            {
                editor.Services.Editor.Delete(editor.Document, editor.History, range);
                if (value.Length > 0) editor.Services.Editor.InsertText(editor.Document, editor.History, range.Start, value);
            }
            editor.Normalize();
            editor.Refresh();
            count = found.Count;
        });
        return count;
    }

    public bool InsertTable(double rows, double columns)
    {
        var r = Whole(rows, nameof(rows));
        var c = Whole(columns, nameof(columns));
        if (r < 1 || r > MaximumTableRows)
            throw new ArgumentOutOfRangeException(nameof(rows), $"A table has from 1 to {MaximumTableRows} rows, not {Number(rows)}.");
        if (c < 1 || c > MaximumTableColumns)
            throw new ArgumentOutOfRangeException(nameof(columns), $"A table has from 1 to {MaximumTableColumns} columns, not {Number(columns)}.");
        Change(() =>
        {
            var editor = AtCursor();
            if (!editor.Insert((writer, at) => writer.InsertTable(editor.Document, editor.History, at, r, c), "insert-table"))
                throw new InvalidOperationException("A table cannot be inserted at the cursor.");
        });
        return true;
    }

    public string Cell(double table, double row, double column) =>
        Read(() => CellParagraph(table, row, column).Text);

    public bool SetCell(double table, double row, double column, string text)
    {
        Change(() =>
        {
            var editor = Writable();
            var paragraph = CellParagraph(table, row, column);
            var cell = $"Cell {Number(row)},{Number(column)} of table {Number(table)}";
            Unguarded(editor, paragraph, cell);
            if (!editor.Replace(DocumentRange.In(paragraph.Id, 0, paragraph.Length), text ?? string.Empty))
                throw new InvalidOperationException($"{cell} contains protected content and cannot be replaced.");
        });
        return true;
    }

    public IReadOnlyList<string> Bookmarks() =>
        Read(() => (IReadOnlyList<string>)[.. AllParagraphs().SelectMany(paragraph => paragraph.Marks.OfType<BookmarkMark>())
            .Select(mark => mark.Name).Distinct(StringComparer.Ordinal)]);

    public bool AddBookmark(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A bookmark name is required.", nameof(name));
        var done = false;
        Change(() =>
        {
            var editor = AtCursor();
            if (Bookmarks().Contains(name, StringComparer.Ordinal)) return;
            editor.Change(() => editor.Services.Editor.AddBookmark(editor.Document, editor.History, editor.Selection, name));
            done = true;
        });
        return done;
    }

    public bool GoToBookmark(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A bookmark name is required.", nameof(name));
        Show(() =>
        {
            var editor = Open();
            foreach (var paragraph in AllParagraphs())
            {
                if (paragraph.Marks.OfType<BookmarkMark>().FirstOrDefault(mark => mark.Name == name) is not { } mark) continue;
                var at = new DocumentPosition(paragraph.Id, Math.Min(mark.Offset, paragraph.Length));
                editor.Select(at, at);
                return;
            }
            var existing = Bookmarks();
            throw new ArgumentException(existing.Count == 0
                ? $"There is no bookmark '{name}': the document has no bookmarks."
                : $"There is no bookmark '{name}'. The document has: {string.Join(", ", existing)}.", nameof(name));
        });
        return true;
    }

    public bool InsertField(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind) || !FieldNames.TryGetValue(kind, out var field))
            throw new ArgumentException($"'{kind}' is not a field kind. Use one of: {string.Join(", ", FieldNames.Keys)}.", nameof(kind));
        Change(() =>
        {
            var editor = AtCursor();
            if (!editor.Insert((writer, at) => writer.InsertField(editor.Document, editor.History, at, field, "0"), "insert-field"))
                throw new InvalidOperationException("A field cannot be inserted at the cursor.");
            services.Fields.UpdateFields(editor);
        });
        return true;
    }

    public bool UpdateFields()
    {
        Change(() => services.Fields.UpdateAll(Writable()));
        return true;
    }

    public bool SetTrackChanges(bool on)
    {
        Change(() =>
        {
            var editor = Writable();
            if (editor.Document.Changes.ProtectionKey is not null)
                throw new InvalidOperationException("Change tracking is protected by a password: remove the protection first.");
            if (!on && editor.Services.Guard.ForcesRecording(editor.Document))
                throw new InvalidOperationException("The document is protected so that every change is tracked: change tracking cannot be turned off.");
            var log = editor.Document.Changes;
            if (log.IsRecording == on) return;
            editor.History.ApplyOutsideHistory(() => log.IsRecording = on);
            editor.Refresh();
        });
        return true;
    }

    public int AcceptAll() => Resolve(RevisionVerdict.Accept);

    public int RejectAll() => Resolve(RevisionVerdict.Reject);

    private FolioEditor? Editor() => context.Target?.Editor;

    private FolioEditor Open() => Editor() ?? throw new InvalidOperationException("No text document is open.");

    private FolioEditor Writable()
    {
        var editor = Open();
        if (editor.ReadOnly()) throw new InvalidOperationException("The document is read-only.");
        return editor;
    }

    private FolioEditor AtCursor()
    {
        var editor = Writable();
        if (editor.Locked())
            throw new InvalidOperationException($"The cursor is in content that cannot be changed: {Reason(editor.Verdict())}.");
        return editor;
    }

    private void Unguarded(FolioEditor editor, ParagraphBlock paragraph, string what)
    {
        var verdict = Verdict(editor, paragraph.Id);
        if (verdict != EditVerdict.Allowed) throw new InvalidOperationException($"{what} cannot be changed: {Reason(verdict)}.");
    }

    private EditVerdict Verdict(FolioEditor editor, string paragraphId) => editor.Services.Guard.Check(editor.Document, paragraphId);

    private string Reason(EditVerdict verdict) => verdict switch
    {
        EditVerdict.Generated => "it is generated content, such as an index or a table of contents",
        EditVerdict.ProtectedSection => "it is in a protected section",
        EditVerdict.ProtectedDocument => "the document is protected",
        EditVerdict.ProtectedCell => "it is in a protected table cell",
        _ => "it is protected",
    };

    private int Resolve(RevisionVerdict verdict)
    {
        var count = 0;
        Change(() =>
        {
            var editor = Writable();
            if (!editor.Services.Guard.CanResolveChanges(editor.Document))
                throw new InvalidOperationException("Tracked changes cannot be accepted or rejected: the document or its change tracking is protected.");
            count = services.Revisions.ResolveAll(editor.Document, editor.History, verdict);
            editor.Normalize();
            editor.Refresh();
        });
        return count;
    }

    private bool Character(double number, PulseOffice.Documents.Styles.CharacterProperties properties)
    {
        Change(() =>
        {
            var editor = Writable();
            var paragraph = Existing(number);
            Unguarded(editor, paragraph, $"Paragraph {Number(number)}");
            editor.Change(() => editor.Services.Editor.SetCharacter(editor.Document, editor.History, DocumentRange.In(paragraph.Id, 0, paragraph.Length), properties));
        });
        return true;
    }

    private int Offset(double value, string name, int minimum, ParagraphBlock paragraph, double number)
    {
        var offset = Whole(value, name);
        if (offset < minimum || offset > paragraph.Length)
            throw new ArgumentOutOfRangeException(name, $"{name} must be between {minimum} and {paragraph.Length}, not {Number(value)}: paragraph {Number(number)} has {paragraph.Length} characters, positions from 0.");
        return offset;
    }

    private int Whole(double value, string name) =>
        double.IsFinite(value) ? (int)Math.Round(value) : throw new ArgumentException($"{name} must be a number, not {Number(value)}.", name);

    private ParagraphBlock Existing(double number)
    {
        var all = Paragraphs();
        var index = Whole(number, "paragraph") - 1;
        return index >= 0 && index < all.Count
            ? all[index]
            : throw new ArgumentOutOfRangeException("paragraph", $"There is no paragraph {Number(number)}: the document has {all.Count} paragraphs, numbered from 1.");
    }

    private string Number(double value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private IReadOnlyList<ParagraphBlock> Paragraphs() =>
        Editor() is { } editor ? [.. Outside(editor.Document.Body)] : [];

    private IEnumerable<ParagraphBlock> Outside(BlockContainer container)
    {
        foreach (var block in container.Blocks)
        {
            if (block is ParagraphBlock paragraph) yield return paragraph;
            else if (block is SectionBlock section)
                foreach (var inner in Outside(section.Content)) yield return inner;
        }
    }

    private IEnumerable<ParagraphBlock> AllParagraphs() =>
        Editor() is { } editor ? services.Tree.Paragraphs(editor.Document.Body) : [];

    private IReadOnlyList<TableBlock> Tables() =>
        Editor() is { } editor ? [.. services.Tree.Blocks(editor.Document.Body).OfType<TableBlock>()] : [];

    private ParagraphBlock CellParagraph(double table, double row, double column)
    {
        var tables = Tables();
        var t = Whole(table, nameof(table)) - 1;
        var r = Whole(row, nameof(row)) - 1;
        var c = Whole(column, nameof(column)) - 1;
        if (t < 0 || t >= tables.Count)
            throw new ArgumentOutOfRangeException(nameof(table), $"There is no table {Number(table)}: the document has {tables.Count} tables, numbered from 1.");
        var rows = tables[t].Rows;
        if (r < 0 || r >= rows.Count)
            throw new ArgumentOutOfRangeException(nameof(row), $"There is no row {Number(row)} in table {Number(table)}: it has {rows.Count} rows, numbered from 1.");
        var cells = rows[r].Cells;
        if (c < 0 || c >= cells.Count)
            throw new ArgumentOutOfRangeException(nameof(column), $"There is no column {Number(column)} in row {Number(row)} of table {Number(table)}: it has {cells.Count} columns, numbered from 1.");
        if (cells[c].Covered)
            throw new ArgumentException($"Cell {Number(row)},{Number(column)} of table {Number(table)} is covered by a merged cell: use the cell where the merge starts.", nameof(column));
        return cells[c].Content.Blocks.OfType<ParagraphBlock>().FirstOrDefault()
            ?? throw new InvalidOperationException($"Cell {Number(row)},{Number(column)} of table {Number(table)} has no paragraph.");
    }
}

public sealed class FolioScriptApi : IScriptApiSection
{
    public IEnumerable<ApiRootSpec> Roots { get; } =
    [
        new(ModuleKind.Documents, "doc", "TextDocument",
        [
            new("Name", "string", "Name: string", []),
            new("Text", "string", "Text: string", []),
            new("ParagraphCount", "number", "ParagraphCount: number", []),
            new("WordCount", "number", "WordCount: number", []),
            new("TableCount", "number", "TableCount: number", []),
            new("MacroCount", "number", "MacroCount: number", []),
            new("SelectedText", "string", "SelectedText: string", []),
            new("TrackChanges", "boolean", "TrackChanges: boolean", []),
            new("ChangeCount", "number", "ChangeCount: number", []),
            new("Paragraph", "string", "Paragraph(paragraph)", ["paragraph:paragraphNumber"]),
            new("SetParagraph", "boolean", "SetParagraph(paragraph, text)", ["paragraph:paragraphNumber", "text:paragraphText"]),
            new("AppendParagraph", "number", "AppendParagraph(text)", ["text:paragraphText"]),
            new("InsertText", "boolean", "InsertText(text)", ["text:insertedText"]),
            new("Style", "string", "Style(paragraph)", ["paragraph:paragraphNumber"]),
            new("SetStyle", "boolean", "SetStyle(paragraph, style)", ["paragraph:paragraphNumber", "style:styleName"]),
            new("Styles", "string[]", "Styles()", []),
            new("Bold", "boolean", "Bold(paragraph, on)", ["paragraph:paragraphNumber", "on:flag"]),
            new("Italic", "boolean", "Italic(paragraph, on)", ["paragraph:paragraphNumber", "on:flag"]),
            new("SelectParagraph", "boolean", "SelectParagraph(paragraph)", ["paragraph:paragraphNumber"]),
            new("Select", "boolean", "Select(paragraph, start, end)", ["paragraph:paragraphNumber", "start:offset", "end:offset"]),
            new("Find", "number", "Find(text)", ["text:text"]),
            new("Replace", "number", "Replace(text, replacement)", ["text:text", "replacement:replacement"]),
            new("InsertTable", "boolean", "InsertTable(rows, columns)", ["rows:count", "columns:count"]),
            new("Cell", "string", "Cell(table, row, column)", ["table:tableNumber", "row:rowNumber", "column:columnNumber"]),
            new("SetCell", "boolean", "SetCell(table, row, column, text)", ["table:tableNumber", "row:rowNumber", "column:columnNumber", "text:cellText"]),
            new("Bookmarks", "string[]", "Bookmarks()", []),
            new("AddBookmark", "boolean", "AddBookmark(name)", ["name:bookmarkName"]),
            new("GoToBookmark", "boolean", "GoToBookmark(name)", ["name:bookmarkName"]),
            new("InsertField", "boolean", "InsertField(kind)", ["kind:fieldKind"]),
            new("UpdateFields", "boolean", "UpdateFields()", []),
            new("SetTrackChanges", "boolean", "SetTrackChanges(on)", ["on:flag"], OutsideHistory: true),
            new("AcceptAll", "number", "AcceptAll()", []),
            new("RejectAll", "number", "RejectAll()", []),
        ]),
    ];
}

public sealed class FolioScriptGlobals(
    IScriptDispatcher dispatcher,
    IScriptOutput output,
    IStatusBarService status,
    FolioScriptHost host,
    IScriptCommands commands,
    FolioFacade doc,
    ScriptFilesFacade files) : IModuleScriptGlobals
{
    public ModuleKind Module => ModuleKind.Documents;

    public IReadOnlyDictionary<string, object> Globals { get; } = new Dictionary<string, object>
    {
        ["app"] = new AppFacade(dispatcher, output, status, host, commands),
        ["doc"] = doc,
        ["files"] = files,
    };
}
