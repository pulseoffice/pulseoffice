using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing.Tables;

public enum TableTextSeparator
{
    Tab,
    Semicolon,
    Paragraph,
    Custom,
}

public sealed record TextTableOptions(TableTextSeparator Separator, char Custom = ',', bool Heading = false);

public sealed record TableConversionResult(DocumentEdit Edit, string? FirstParagraphId);

public interface ITableConversion
{
    bool CanConvertToTable(TextDocument document, DocumentRange range);

    TableConversionResult ToTable(TextDocument document, UndoStack history, DocumentRange range, TextTableOptions options, double width);

    TableConversionResult ToText(TextDocument document, UndoStack history, TableCellLocation at, TextTableOptions options);

    TableConversionResult SplitTable(TextDocument document, UndoStack history, TableCellLocation at, bool copyHeading);

    bool CanMergeWithNext(TableCellLocation at);

    DocumentEdit MergeWithNext(TextDocument document, UndoStack history, TableCellLocation at);
}

public sealed class TableConversion(IStoryCatalog stories, IParagraphText text, ITableCellFactory cells, IRangeDeletion deletion, ITableSpanCutting spans) : ITableConversion
{
    public bool CanConvertToTable(TextDocument document, DocumentRange range) => Paragraphs(document, range) is { Count: > 0 };

    public TableConversionResult ToTable(TextDocument document, UndoStack history, DocumentRange range, TextTableOptions options, double width)
    {
        var paragraphs = Paragraphs(document, range) ?? throw new InvalidOperationException("The selection does not span sibling paragraphs");
        var owner = stories.Find(document, paragraphs[0].Id).Location.Container;
        var index = owner.IndexOf(paragraphs[0]);
        var recorder = new EditRecorder(document, "text-to-table").Touch(owner);
        List<IReadOnlyList<ParagraphState>> rows = options.Separator == TableTextSeparator.Paragraph
            ? [.. paragraphs.Select(paragraph => (IReadOnlyList<ParagraphState>)[paragraph.State])]
            : [.. paragraphs.Select(paragraph => (IReadOnlyList<ParagraphState>)Pieces(paragraph.State, Separator(options)))];
        var columns = Math.Max(1, rows.Max(row => row.Count));
        var table = new TableBlock(document.NextId("t"),
            rows.Select((pieces, r) => new TableRow(Enumerable.Range(0, columns).Select(c => Cell(document, pieces, c, options.Heading && r == 0)))),
            Enumerable.Repeat(Twip.FromPoints(width / columns), columns))
        {
            StyleName = DefaultStyleNames.DefaultTableStyle,
            HeaderRows = options.Heading && rows.Count > 1 ? 1 : 0,
        };
        owner.RemoveRange(index, paragraphs.Count);
        owner.Insert(index, table);
        recorder.Record(new BlockRestored(table.Id));
        var first = table.Rows[0].Cells[0].Content.Blocks.OfType<ParagraphBlock>().First().Id;
        return new TableConversionResult(recorder.Commit(history), first);
    }

    public TableConversionResult ToText(TextDocument document, UndoStack history, TableCellLocation at, TextTableOptions options)
    {
        var owner = at.Owner;
        var index = owner.IndexOf(at.Table);
        var recorder = new EditRecorder(document, "table-to-text").Touch(owner);
        var produced = new List<Block>();
        foreach (var row in at.Table.Rows)
        {
            var contents = row.Cells.Where(cell => !cell.Covered).Select(cell => cell.Content.Blocks.OfType<ParagraphBlock>().ToList()).ToList();
            if (options.Separator == TableTextSeparator.Paragraph)
            {
                produced.AddRange(contents.SelectMany(list => list));
                continue;
            }
            ParagraphBlock? line = null;
            var extras = new List<Block>();
            foreach (var list in contents)
            {
                var head = list.FirstOrDefault() ?? new ParagraphBlock(document.NextId("p"), string.Empty, DefaultStyleNames.Standard);
                if (line is null)
                {
                    line = new ParagraphBlock(head.Id, head.Content) { Marks = head.Marks };
                    line.Content = line.Content with { StyleName = Restyled(line.StyleName) };
                }
                else
                {
                    var joined = text.Insert(line.State, line.Length, Separator(options).ToString());
                    line.State = text.Concat(joined, head.State);
                }
                extras.AddRange(list.Skip(1));
            }
            if (line is not null) produced.Add(line);
            produced.AddRange(extras);
        }
        if (produced.Count == 0) produced.Add(new ParagraphBlock(document.NextId("p"), string.Empty, DefaultStyleNames.Standard));
        owner.RemoveAt(index);
        owner.InsertRange(index, produced);
        foreach (var block in produced) recorder.Record(new BlockRestored(block.Id));
        return new TableConversionResult(recorder.Commit(history), produced.OfType<ParagraphBlock>().FirstOrDefault()?.Id);
    }

    public TableConversionResult SplitTable(TextDocument document, UndoStack history, TableCellLocation at, bool copyHeading)
    {
        var table = at.Table;
        var owner = at.Owner;
        var recorder = new EditRecorder(document, "split-table").Touch(owner).Touch(table);
        var row = Math.Max(1, at.Row);
        var moved = table.Rows.Skip(row).ToList();
        if (moved.Count > 0)
            foreach (var heir in spans.CutAbove(document, table, row).Where(heir => heir.Column < moved[0].Cells.Count))
                moved[0].Cells[heir.Column] = heir.Cell;
        table.Rows.RemoveRange(row, moved.Count);
        var movedHeading = Math.Max(0, table.HeaderRows - row);
        table.HeaderRows = Math.Min(table.HeaderRows, row);
        var heading = copyHeading && movedHeading == 0 ? table.Rows.Take(table.HeaderRows).Select(source => Clone(document, source)).ToList() : [];
        var second = new TableBlock(document.NextId("t"), [.. heading, .. moved], table.Columns)
        {
            StyleName = table.StyleName,
            HeaderRows = heading.Count + movedHeading,
            RepeatHeaderRows = table.RepeatHeaderRows,
            Direct = table.Direct,
            Alignment = table.Alignment,
            Indent = table.Indent,
        };
        var gap = new ParagraphBlock(document.NextId("p"), string.Empty, DefaultStyleNames.Standard);
        var index = owner.IndexOf(table);
        owner.InsertRange(index + 1, [gap, second]);
        recorder.Record(new BlockRestored(gap.Id)).Record(new BlockRestored(second.Id));
        return new TableConversionResult(recorder.Commit(history), gap.Id);
    }

    public bool CanMergeWithNext(TableCellLocation at) => Next(at) is not null;

    public DocumentEdit MergeWithNext(TextDocument document, UndoStack history, TableCellLocation at)
    {
        var (gap, next) = Next(at) ?? throw new InvalidOperationException("There is no table to merge with");
        var owner = at.Owner;
        var recorder = new EditRecorder(document, "merge-tables").Touch(owner).Touch(at.Table).Touch(next);
        var columns = Math.Max(at.Table.ColumnCount, next.ColumnCount);
        foreach (var row in at.Table.Rows.Concat(next.Rows))
            while (row.Cells.Count < columns) row.Cells.Add(cells.Create(document));
        while (at.Table.Columns.Count < columns) at.Table.Columns.Add(next.Columns[at.Table.Columns.Count]);
        var dropped = next.HeaderRows > 0 && SameHeading(at.Table, next) ? Math.Min(next.HeaderRows, next.Rows.Count) : 0;
        deletion.Discard(document, recorder, next.Rows.Take(dropped).SelectMany(row => row.Cells).SelectMany(cell => cell.Content.Blocks));
        at.Table.Rows.AddRange(next.Rows.Skip(dropped));
        owner.Remove(next);
        if (gap is not null) owner.Remove(gap);
        return recorder.Commit(history);
    }

    private bool SameHeading(TableBlock first, TableBlock second) =>
        first.HeaderRows == second.HeaderRows && first.Rows.Take(first.HeaderRows).Zip(second.Rows.Take(second.HeaderRows))
            .All(pair => pair.First.Cells.Count == pair.Second.Cells.Count && pair.First.Cells.Zip(pair.Second.Cells)
                .All(cell => string.Join("\n", cell.First.Content.Blocks.OfType<ParagraphBlock>().Select(p => p.Text)) == string.Join("\n", cell.Second.Content.Blocks.OfType<ParagraphBlock>().Select(p => p.Text))));

    private (ParagraphBlock? Gap, TableBlock Table)? Next(TableCellLocation at)
    {
        var index = at.Owner.IndexOf(at.Table);
        if (index + 1 < at.Owner.Count && at.Owner[index + 1] is TableBlock adjacent) return (null, adjacent);
        if (index + 2 < at.Owner.Count && at.Owner[index + 1] is ParagraphBlock { Length: 0 } gap && at.Owner[index + 2] is TableBlock after) return (gap, after);
        return null;
    }

    private TableRow Clone(TextDocument document, TableRow source) =>
        new(source.Cells.Select(cell =>
        {
            var copy = cell.Clone();
            copy.Content.Replace(cell.Content.Blocks.Select(block => block is ParagraphBlock paragraph ? new ParagraphBlock(document.NextId("p"), paragraph.Content) : block.Clone()));
            return copy;
        }))
        {
            Height = source.Height,
            MinimumHeight = source.MinimumHeight,
            AllowBreakAcrossPages = source.AllowBreakAcrossPages,
        };

    private TableCell Cell(TextDocument document, IReadOnlyList<ParagraphState> pieces, int column, bool heading)
    {
        var style = heading ? DefaultStyleNames.TableHeading : DefaultStyleNames.TableContents;
        if (column >= pieces.Count) return cells.Create(document, string.Empty, style);
        var piece = pieces[column];
        var paragraph = new ParagraphBlock(document.NextId("p"), piece.Content with { StyleName = Cellular(piece.Content.StyleName, style) }) { Marks = piece.Marks };
        return new TableCell(new BlockContainer([paragraph]));
    }

    private string? Cellular(string? styleName, string style) =>
        styleName is null or DefaultStyleNames.Standard or DefaultStyleNames.TextBody ? style : styleName;

    private string? Restyled(string? styleName) =>
        styleName is DefaultStyleNames.TableContents or DefaultStyleNames.TableHeading ? DefaultStyleNames.Standard : styleName;

    private List<ParagraphState> Pieces(ParagraphState state, char separator)
    {
        var pieces = new List<ParagraphState>();
        var rest = state;
        while (SeparatorOutsideFields(rest, separator) is var at and >= 0)
        {
            var (left, right) = text.Split(rest, at);
            pieces.Add(left);
            rest = text.Delete(right, 0, 1);
        }
        pieces.Add(rest);
        return pieces;
    }

    private int SeparatorOutsideFields(ParagraphState state, char separator)
    {
        var position = 0;
        foreach (var run in text.Runs(state.Content))
        {
            if (run.Field == TextFieldKind.None && run.Text.IndexOf(separator) is var inside and >= 0) return position + inside;
            position += run.Text.Length;
        }
        return -1;
    }

    private char Separator(TextTableOptions options) => options.Separator switch
    {
        TableTextSeparator.Tab => '\t',
        TableTextSeparator.Semicolon => ';',
        TableTextSeparator.Paragraph => '\n',
        _ => options.Custom,
    };

    private List<ParagraphBlock>? Paragraphs(TextDocument document, DocumentRange range)
    {
        var start = stories.Find(document, range.Start.ParagraphId).Location;
        var end = stories.Find(document, range.End.ParagraphId).Location;
        if (!ReferenceEquals(start.Container, end.Container) || end.Index < start.Index) return null;
        var blocks = start.Container.Blocks.Skip(start.Index).Take(end.Index - start.Index + 1).ToList();
        return blocks.All(block => block is ParagraphBlock) ? [.. blocks.Cast<ParagraphBlock>()] : null;
    }
}
