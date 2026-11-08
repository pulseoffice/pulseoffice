using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Editing;

public abstract class Memento
{
    public abstract object Target { get; }

    public abstract Memento Capture();

    public abstract void Restore();

    public abstract bool IsCurrent();
}

public sealed class ParagraphMemento(ParagraphBlock paragraph) : Memento
{
    private readonly ParagraphState _state = paragraph.State;

    public override object Target => paragraph;

    public override Memento Capture() => new ParagraphMemento(paragraph);

    public override void Restore() => paragraph.State = _state;

    public override bool IsCurrent()
    {
        var current = paragraph.State;
        if (ReferenceEquals(current, _state)) return true;
        return current.Marks.SequenceEqual(_state.Marks)
            && current.Content.Runs.SequenceEqual(_state.Content.Runs)
            && current.Content with { Runs = _state.Content.Runs } == _state.Content;
    }
}

public sealed class ContainerMemento(BlockContainer container) : Memento
{
    private readonly IReadOnlyList<Block> _blocks = [.. container.Blocks];

    public override object Target => container;

    public override Memento Capture() => new ContainerMemento(container);

    public override void Restore() => container.Replace(_blocks);

    public override bool IsCurrent() => container.Blocks.SequenceEqual(_blocks, ReferenceEqualityComparer.Instance);
}

public sealed class ListMemento<T>(object owner, Func<IReadOnlyList<T>> read, Action<IReadOnlyList<T>> write) : Memento
{
    private readonly IReadOnlyList<T> _items = [.. read()];

    public override object Target => owner;

    public override Memento Capture() => new ListMemento<T>(owner, read, write);

    public override void Restore() => write(_items);

    public override bool IsCurrent() => read().SequenceEqual(_items);
}

public sealed class ObjectMemento(Objects.AnchoredObject item) : Memento
{
    private readonly Objects.AnchoredObject _state = item.CopyAs(item.Id, item.Anchor, item.Content);

    public override object Target => item;

    public override Memento Capture() => new ObjectMemento(item);

    public override void Restore() => item.Assign(_state);

    public override bool IsCurrent() => item.SameState(_state);
}

public sealed class PropertiesMemento : Memento
{
    private readonly PulseOffice.Document.Metadata.DocumentProperties _properties;

    private readonly (string? Title, string? Subject, string? Description, string? Author, string? LastModifiedBy) _fields;

    private readonly IReadOnlyList<string> _keywords;

    private readonly IReadOnlyDictionary<string, string> _custom;

    public PropertiesMemento(PulseOffice.Document.Metadata.DocumentProperties properties)
    {
        _properties = properties;
        _fields = Fields();
        _keywords = [.. properties.Keywords];
        _custom = new Dictionary<string, string>(properties.Custom, StringComparer.Ordinal);
    }

    public override object Target => _properties;

    public override Memento Capture() => new PropertiesMemento(_properties);

    public override void Restore()
    {
        (_properties.Title, _properties.Subject, _properties.Description, _properties.Author, _properties.LastModifiedBy) = _fields;
        _properties.Keywords.Clear();
        _properties.Keywords.AddRange(_keywords);
        _properties.Custom.Clear();
        foreach (var (name, value) in _custom) _properties.Custom[name] = value;
    }

    public override bool IsCurrent() =>
        Fields() == _fields
        && _properties.Keywords.SequenceEqual(_keywords)
        && _properties.Custom.Count == _custom.Count
        && _properties.Custom.All(entry => _custom.TryGetValue(entry.Key, out var value) && value == entry.Value);

    private (string?, string?, string?, string?, string?) Fields() =>
        (_properties.Title, _properties.Subject, _properties.Description, _properties.Author, _properties.LastModifiedBy);
}

public sealed class TableMemento(TableBlock table) : Memento
{
    private sealed record CellState(TableCell Cell, int ColumnSpan, int RowSpan, bool Covered, string? StyleName, uint? Background, CellVerticalAlign VerticalAlign, PulseOffice.Text.Rich.BoxBorders? Borders, string? Formula, bool Protected, CellNumberFormat? NumberFormat, CellTextDirection TextDirection, CellPadding? Padding)
    {
        public CellState(TableCell cell) : this(cell, cell.ColumnSpan, cell.RowSpan, cell.Covered, cell.StyleName, cell.Background, cell.VerticalAlign, cell.Borders, cell.Formula, cell.Protected, cell.NumberFormat, cell.TextDirection, cell.Padding)
        {
        }

        public void Restore()
        {
            Cell.ColumnSpan = ColumnSpan;
            Cell.RowSpan = RowSpan;
            Cell.Covered = Covered;
            Cell.StyleName = StyleName;
            Cell.Background = Background;
            Cell.VerticalAlign = VerticalAlign;
            Cell.Borders = Borders;
            Cell.Formula = Formula;
            Cell.Protected = Protected;
            Cell.NumberFormat = NumberFormat;
            Cell.TextDirection = TextDirection;
            Cell.Padding = Padding;
        }
    }

    private sealed record RowState(TableRow Row, IReadOnlyList<CellState> Cells, Foundation.Units.Twip? Height, bool MinimumHeight, bool AllowBreak, string? ChangeId)
    {
        public RowState(TableRow row) : this(row, [.. row.Cells.Select(cell => new CellState(cell))], row.Height, row.MinimumHeight, row.AllowBreakAcrossPages, row.ChangeId)
        {
        }

        public bool Matches(TableRow row) =>
            ReferenceEquals(row, Row) && row.Height == Height && row.MinimumHeight == MinimumHeight && row.AllowBreakAcrossPages == AllowBreak && row.ChangeId == ChangeId
            && row.Cells.Count == Cells.Count && row.Cells.Zip(Cells).All(pair => new CellState(pair.First) == pair.Second);
    }

    private readonly IReadOnlyList<RowState> _rows = [.. table.Rows.Select(row => new RowState(row))];

    private readonly IReadOnlyList<Foundation.Units.Twip> _columns = [.. table.Columns];

    private readonly (string? Name, string? StyleName, int HeaderRows, bool RepeatHeaderRows, TableAlignment Alignment, Foundation.Units.Twip Indent) _table = (table.Name, table.StyleName, table.HeaderRows, table.RepeatHeaderRows, table.Alignment, table.Indent);

    public override object Target => table;

    public override Memento Capture() => new TableMemento(table);

    public override void Restore()
    {
        table.Rows.Clear();
        foreach (var state in _rows)
        {
            var row = state.Row;
            row.Cells.Clear();
            foreach (var cell in state.Cells)
            {
                cell.Restore();
                row.Cells.Add(cell.Cell);
            }
            row.Height = state.Height;
            row.MinimumHeight = state.MinimumHeight;
            row.AllowBreakAcrossPages = state.AllowBreak;
            row.ChangeId = state.ChangeId;
            table.Rows.Add(row);
        }
        table.Columns.Clear();
        table.Columns.AddRange(_columns);
        (table.Name, table.StyleName, table.HeaderRows, table.RepeatHeaderRows, table.Alignment, table.Indent) = _table;
    }

    public override bool IsCurrent() =>
        table.Columns.SequenceEqual(_columns)
        && (table.Name, table.StyleName, table.HeaderRows, table.RepeatHeaderRows, table.Alignment, table.Indent) == _table
        && table.Rows.Count == _rows.Count
        && table.Rows.Zip(_rows).All(pair => pair.Second.Matches(pair.First));
}

public sealed class DocumentEdit : IUndoableAction
{
    private readonly TextDocument _document;
    private readonly IReadOnlyList<Memento> _before;
    private readonly IReadOnlyList<Memento> _after;
    private readonly IReadOnlyList<TextChange> _changes;

    public DocumentEdit(TextDocument document, string description, IReadOnlyList<Memento> before, IReadOnlyList<TextChange> changes)
    {
        _document = document;
        Description = description;
        _before = before;
        _after = [.. before.Select(memento => memento.Capture())];
        _changes = changes;
    }

    public string Description { get; }

    public IReadOnlyList<TextChange> Changes => _changes;

    public void Redo()
    {
        foreach (var memento in _after) memento.Restore();
        _document.Raise(_changes);
    }

    public void Undo()
    {
        foreach (var memento in _before.Reverse()) memento.Restore();
        _document.Raise([.. _changes.Reverse().Select(change => change.Inverse())]);
    }
}

public sealed class EditRecorder(TextDocument document, string description)
{
    private readonly List<Memento> _before = [];
    private readonly HashSet<object> _touched = new(ReferenceEqualityComparer.Instance);
    private readonly List<TextChange> _changes = [];

    public EditRecorder Touch(ParagraphBlock paragraph) => Keep(new ParagraphMemento(paragraph));

    public EditRecorder Touch(BlockContainer container) => Keep(new ContainerMemento(container));

    public EditRecorder Touch(TableBlock table) => Keep(new TableMemento(table));

    public EditRecorder Touch(Objects.AnchoredObject item) => Keep(new ObjectMemento(item));

    public EditRecorder Touch(PulseOffice.Document.Metadata.DocumentProperties properties) => Keep(new PropertiesMemento(properties));

    public EditRecorder Touch<T>(object owner, Func<IReadOnlyList<T>> read, Action<IReadOnlyList<T>> write) =>
        Keep(new ListMemento<T>(owner, read, write));

    public bool HasChanges => _changes.Count > 0 || _before.Any(memento => !memento.IsCurrent());

    public EditRecorder Record(TextChange change)
    {
        _changes.Add(change);
        return this;
    }

    public DocumentEdit Commit(UndoStack history)
    {
        var edit = new DocumentEdit(document, description, _before, _changes);
        if (_changes.Count == 0 && _before.All(memento => memento.IsCurrent())) return edit;
        history.Push(edit);
        document.Raise(_changes);
        return edit;
    }

    private EditRecorder Keep(Memento memento)
    {
        if (_touched.Add(memento.Target)) _before.Add(memento);
        return this;
    }
}
