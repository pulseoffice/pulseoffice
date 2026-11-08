using CellVerticalAlign = PulseOffice.Documents.Blocks.CellVerticalAlign;
using PulseOffice.Document.Embedding;
using PulseOffice.Documents;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Interchange.Text.Assembly;

public sealed record ParagraphShape
{
    public static readonly ParagraphShape Plain = new();

    public string? StyleName { get; init; }

    public ParagraphProperties Properties { get; init; } = ParagraphProperties.None;

    public string? ListStyleName { get; init; }

    public bool Bullet { get; init; }

    public bool Numbered { get; init; }

    public int Level { get; init; }

    public string? ListId { get; init; }

    public string? ListSegment { get; init; }

    public int? ListSegmentStart { get; init; }
    public bool ListHeader { get; init; }
}

public sealed record RunShape
{
    public static readonly RunShape Plain = new();

    public CharacterProperties Character { get; init; } = CharacterProperties.None;

    public string? Hyperlink { get; init; }

    public string? StyleName { get; init; }

    public TextFieldKind Field { get; init; }

    public string? FieldArgument { get; init; }
}

public sealed record CellShape
{
    public int ColumnSpan { get; init; } = 1;

    public int RowSpan { get; init; } = 1;

    public uint? Background { get; init; }

    public CellVerticalAlign VerticalAlign { get; init; }

    public bool MergeUp { get; init; }

    public BoxBorders? Borders { get; init; }
}

public sealed class TextDocumentAssembler
{
    private readonly IDirectFormatting _direct;
    private readonly Stack<Scope> _scopes = new();
    private readonly Stack<TableDraft> _tables = new();
    private readonly HashSet<string> _listSegments = new(StringComparer.Ordinal);
    private int _entered;

    public TextDocumentAssembler(TextDocument document, IDirectFormatting direct)
    {
        Document = document;
        _direct = direct;
        document.Body.Replace([]);
        _scopes.Push(new Scope(document.Body));
    }

    public TextDocument Document { get; }

    public int Depth => _scopes.Count + _tables.Count;

    public bool TablesTruncated { get; private set; }

    private readonly ImportedCellBudget _tableCells = new(ImportedTableLimits.DocumentTableCells);

    public bool HasPendingContent => Current.Runs.Count > 0 || Current.Marks.Count > 0 || Current.Anchored;

    public bool InTable => _tables.Count > 0 && _tables.Peek().Depth == _scopes.Count - 1;

    private Scope Current => _scopes.Peek();

    public void Write(string text, RunShape shape)
    {
        if (text.Length == 0) return;
        var scope = Current;
        if (scope.Runs.Count > 0 && scope.Runs[^1].Shape == shape && shape.Field == TextFieldKind.None) scope.Runs[^1].Text.Append(text);
        else scope.Runs.Add(new RunDraft(new System.Text.StringBuilder(text), shape));
        scope.Length += text.Length;
    }

    public void TrimTrailingSpace()
    {
        var scope = Current;
        var runs = scope.Runs;
        while (runs.Count > 0 && runs[^1].Shape.Field == TextFieldKind.None && runs[^1].Text is { Length: > 0 } last && last[^1] == ' ')
        {
            var spaces = 0;
            while (spaces < last.Length && last[last.Length - 1 - spaces] == ' ') spaces++;
            if (scope.Marks.Any(mark => mark.Offset > scope.Length - spaces)) return;
            if (spaces == last.Length) runs.RemoveAt(runs.Count - 1);
            else last.Length -= spaces;
            scope.Length -= spaces;
        }
    }

    public void Mark(Func<int, InlineMark> create) => Current.Marks.Add(create(Current.Length));

    public string ParagraphId => Current.Id ??= Document.NextId("p");

    public void EndParagraph(ParagraphShape shape)
    {
        var scope = Current;
        var runs = scope.Runs.Select(Run).ToList();
        var listed = shape.ListStyleName is not null;
        int? restart = listed && shape.ListSegment is { } segment && _listSegments.Add(segment) ? shape.ListSegmentStart : null;
        var paragraph = new TextParagraph(string.Empty)
        {
            StyleName = shape.StyleName ?? DefaultStyleNames.Standard,
            ListStyleName = shape.ListStyleName,
            Bullet = shape.Bullet,
            Numbered = shape.Numbered,
            BulletLevel = Math.Clamp(shape.Level, 0, ListStyle.LevelCount - 1),
            ListId = listed ? shape.ListId : null,
            ListRestart = restart is { } start ? Math.Max(0, start) : null,
            ListHeader = shape.ListHeader && shape.ListStyleName is not null,
        }.WithRuns(runs);
        paragraph = _direct.Set(paragraph, shape.Properties);
        scope.Target.Add(new ParagraphBlock(ParagraphId, paragraph) { Marks = [.. scope.Marks] });
        scope.Reset();
    }

    public void Flush(ParagraphShape shape)
    {
        if (HasPendingContent) EndParagraph(shape);
    }

    public void Image(EmbeddedObject image, DocumentSize size)
    {
        var id = Document.NextId("obj");
        Document.Objects.Add(new AnchoredObject(id, new AsCharacterAnchor(ParagraphId), new EmbeddedContent(image), size));
        Mark(offset => new ObjectMark(offset, id));
    }

    public (string Id, BlockContainer Content) ReserveNote(NoteKind kind)
    {
        var id = Document.NextId(kind == NoteKind.Footnote ? "ftn" : "end");
        Mark(offset => new NoteMark(offset, id));
        var container = new BlockContainer();
        Document.Notes.Add(new Note(id, kind, container));
        return (id, container);
    }

    public BlockContainer BeginTextBox(DocumentSize size, bool autoGrow = true)
    {
        var container = new BlockContainer();
        var id = Document.NextId("frame");
        Document.Objects.Add(new AnchoredObject(id, new ParagraphAnchor(ParagraphId), new TextBoxContent(container) { AutoGrowHeight = autoGrow }, size));
        Current.Anchored = true;
        Enter(container);
        return container;
    }

    public void BeginNote(NoteKind kind) => Enter(ReserveNote(kind).Content);

    public void EndNote(ParagraphShape shape) => Leave(shape);

    public void Enter(BlockContainer container)
    {
        _entered++;
        _scopes.Push(new Scope(container));
    }

    public void Leave(ParagraphShape shape)
    {
        if (_entered == 0 || _scopes.Count < 2) return;
        Flush(shape);
        _scopes.Pop();
        _entered--;
    }

    public void BeginTable()
    {
        _tables.Push(new TableDraft(_scopes.Count));
    }

    public void BeginRow()
    {
        if (_tables.TryPeek(out var table) && table.Depth == _scopes.Count) table.Rows.Add([]);
    }

    public void BeginCell(CellShape shape)
    {
        if (!_tables.TryPeek(out var table) || table.Depth != _scopes.Count) return;
        if (table.Rows.Count == 0) table.Rows.Add([]);
        var container = new BlockContainer();
        table.Rows[^1].Add(new CellDraft(container, shape));
        _scopes.Push(new Scope(container));
    }

    public void EndCell(ParagraphShape shape, bool discard = false)
    {
        if (!_tables.TryPeek(out var table) || table.Depth != _scopes.Count - 1) return;
        Flush(shape);
        _scopes.Pop();
        if (discard && table.Rows.Count > 0 && table.Rows[^1].Count > 0) table.Rows[^1].RemoveAt(table.Rows[^1].Count - 1);
    }

    public void EndBookmark(string name)
    {
        var marks = Current.Marks;
        var offset = Current.Length;
        var start = marks.FindLastIndex(mark => mark is BookmarkMark { Edge: MarkEdge.Start } open && open.Name == name);
        if (start >= 0 && marks[start].Offset == offset) marks[start] = new BookmarkMark(offset, name, MarkEdge.Point);
        else marks.Add(new BookmarkMark(offset, name, MarkEdge.End));
    }

    public void EndTable(IReadOnlyList<Twip>? columns, int headerRows = 0)
    {
        if (!_tables.TryPeek(out var table) || table.Depth != _scopes.Count) return;
        _tables.Pop();
        var block = table.Build(Document.NextId("tbl"), columns, headerRows, _tableCells);
        TablesTruncated |= table.Truncated;
        if (block is not null) Current.Target.Add(block);
    }

    public void Close(ParagraphShape shape)
    {
        while (_tables.Count > 0)
        {
            var depth = _tables.Peek().Depth;
            while (_scopes.Count > depth)
            {
                if (_scopes.Count == depth + 1) EndCell(shape);
                else
                {
                    Flush(shape);
                    _scopes.Pop();
                    if (_entered > 0) _entered--;
                }
            }
            if (_scopes.Count < depth) _tables.Pop();
            else EndTable(null);
        }
        while (_scopes.Count > 1)
        {
            Flush(shape);
            _scopes.Pop();
        }
        Flush(shape);
    }

    private TextRun Run(RunDraft draft)
    {
        var run = new TextRun(draft.Text.ToString())
        {
            Hyperlink = draft.Shape.Hyperlink,
            StyleName = draft.Shape.StyleName,
            Field = draft.Shape.Field,
            FieldArgument = draft.Shape.FieldArgument,
        };
        return _direct.Set(run, draft.Shape.Character);
    }

    private sealed record RunDraft(System.Text.StringBuilder Text, RunShape Shape);

    private sealed class Scope(BlockContainer target)
    {
        public BlockContainer Target { get; } = target;

        public List<RunDraft> Runs { get; } = [];

        public List<InlineMark> Marks { get; } = [];

        public string? Id { get; set; }

        public bool Anchored { get; set; }

        public int Length { get; set; }

        public void Reset()
        {
            Runs.Clear();
            Marks.Clear();
            Length = 0;
            Id = null;
            Anchored = false;
        }
    }

    private sealed record CellDraft(BlockContainer Content, CellShape Shape);

    private sealed class TableDraft(int depth)
    {
        private const int MaximumColumns = ImportedTableLimits.TextTableColumns;
        private const long MaximumCells = ImportedTableLimits.TextTableCells;

        public int Depth { get; } = depth;

        public List<List<CellDraft>> Rows { get; } = [];

        public bool Truncated { get; private set; }

        public TableBlock? Build(string id, IReadOnlyList<Twip>? widths, int headerRows, ImportedCellBudget budget)
        {
            var rows = Rows.Where(row => row.Count > 0).ToList();
            if (rows.Count == 0) return null;
            var grid = new List<TableCell?[]>();
            var columnCount = 0;
            var occupied = new Dictionary<(int Row, int Column), bool>();
            var origins = new Dictionary<(int Row, int Column), TableCell>();
            var kept = rows.Count;
            var starts = new Dictionary<TableCell, int>(ReferenceEqualityComparer.Instance);
            for (var r = 0; r < rows.Count && r < kept; r++)
            {
                var column = 0;
                foreach (var draft in rows[r])
                {
                    while (occupied.ContainsKey((r, column))) column++;
                    if (column >= MaximumColumns)
                    {
                        Truncated = true;
                        break;
                    }
                    if (draft.Shape.MergeUp && origins.TryGetValue((r - 1, column), out var above))
                    {
                        above.RowSpan++;
                        for (var dc = 0; dc < above.ColumnSpan; dc++)
                        {
                            occupied[(r, column + dc)] = false;
                            origins[(r, column + dc)] = above;
                        }
                        column += above.ColumnSpan;
                        columnCount = Math.Max(columnCount, column);
                        continue;
                    }
                    var span = Math.Clamp(draft.Shape.ColumnSpan, 1, MaximumColumns - column);
                    var down = Math.Clamp(draft.Shape.RowSpan, 1, rows.Count - r);
                    if (span < draft.Shape.ColumnSpan) Truncated = true;
                    if ((long)occupied.Count + (long)span * down > MaximumCells)
                    {
                        Truncated = true;
                        kept = r;
                        break;
                    }
                    for (var dr = 0; dr < down; dr++)
                        for (var dc = 0; dc < span; dc++)
                            occupied[(r + dr, column + dc)] = dr == 0 && dc == 0;
                    if (draft.Content.Count == 0) draft.Content.Add(new ParagraphBlock($"{id}_{r}_{column}", string.Empty));
                    Ensure(grid, r);
                    var cell = new TableCell(draft.Content)
                    {
                        ColumnSpan = span,
                        RowSpan = down,
                        Background = draft.Shape.Background,
                        VerticalAlign = draft.Shape.VerticalAlign,
                        Borders = draft.Shape.Borders,
                    };
                    Place(grid, r, column, cell);
                    starts[cell] = r;
                    for (var dr = 0; dr < down; dr++)
                        for (var dc = 0; dc < span; dc++)
                            origins[(r + dr, column + dc)] = cell;
                    column += span;
                    columnCount = Math.Max(columnCount, column);
                }
            }

            if (kept == 0) return null;
            columnCount = Math.Max(columnCount, occupied.Keys.Select(key => key.Column + 1).DefaultIfEmpty(1).Max());
            if ((long)kept * columnCount > MaximumCells)
            {
                Truncated = true;
                kept = (int)(MaximumCells / columnCount);
            }
            var granted = budget.Rows(kept, columnCount);
            if (granted < kept)
            {
                Truncated = true;
                kept = granted;
                if (kept == 0) return null;
            }
            foreach (var (cell, row) in starts) cell.RowSpan = Math.Max(1, Math.Min(cell.RowSpan, kept - row));
            var built = new List<TableRow>();
            for (var r = 0; r < kept; r++)
            {
                Ensure(grid, r);
                var cells = new List<TableCell>();
                for (var c = 0; c < columnCount; c++)
                {
                    var cell = c < grid[r].Length ? grid[r][c] : null;
                    cells.Add(cell ?? new TableCell(occupied.TryGetValue((r, c), out var origin) && !origin
                        ? new BlockContainer()
                        : new BlockContainer([new ParagraphBlock(id + "_" + r + "_" + c, string.Empty)]))
                    {
                        Covered = occupied.TryGetValue((r, c), out var start) && !start,
                    });
                }
                built.Add(new TableRow(cells));
            }

            var columns = widths is { Count: > 0 } && widths.Count == columnCount
                ? widths
                : Enumerable.Repeat(new Twip(Twip.FromCentimeters(17).Value / columnCount), columnCount).ToList();
            return new TableBlock(id, built, columns) { HeaderRows = Math.Clamp(headerRows, 0, built.Count) };
        }

        private void Ensure(List<TableCell?[]> grid, int row)
        {
            while (grid.Count <= row) grid.Add([]);
        }

        private void Place(List<TableCell?[]> grid, int row, int column, TableCell cell)
        {
            if (grid[row].Length <= column)
            {
                var wider = new TableCell?[Math.Max(column + 1, Math.Min(MaximumColumns, grid[row].Length * 2))];
                grid[row].CopyTo(wider, 0);
                grid[row] = wider;
            }
            grid[row][column] = cell;
        }
    }
}
