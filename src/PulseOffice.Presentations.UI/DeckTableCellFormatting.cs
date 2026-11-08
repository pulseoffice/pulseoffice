using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;

namespace PulseOffice.Presentations.UI;

public interface IDeckTableLookSeed
{
    IReadOnlyList<TableCellFormat> Formats(DataTableObject table);
}

public sealed class DeckTableLookSeed : IDeckTableLookSeed
{
    private const string GridHex = "#D3D7DE";
    private const double GridWidth = 0.75;
    private const string BandHex = "#F3F4F6";
    private const string HeaderFillHex = "#E3EEFB";
    private const string DefaultAccent = "#16A34A";

    public IReadOnlyList<TableCellFormat> Formats(DataTableObject table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Style.OwnLook) return table.CellFormats;
        var style = table.Style;
        var index = new TableCellIndex(table);
        var grid = style.Grid ? new TableBorderLine(GridHex, GridWidth) : null;
        var accent = string.IsNullOrEmpty(style.AccentHex) ? DefaultAccent : style.AccentHex;
        var first = style.HeaderRow ? TableCellFormat.HeaderRow : 0;
        var formats = new List<TableCellFormat>();
        for (var row = first; row < table.Snapshot.Rows.Count; row++)
            for (var column = 0; column < table.Snapshot.Columns.Count; column++)
            {
                var header = row < 0;
                formats.Add((index.Format(row, column) ?? new TableCellFormat(row, column, header ? CellVerticalAlign.Center : CellVerticalAlign.Top)) with
                {
                    Look = new TableCellLook
                    {
                        FillHex = header ? HeaderFillHex : style.BandedRows && row % 2 == 1 ? BandHex : null,
                        TextHex = header ? accent : null,
                        Bold = header,
                        FontSize = style.FontSize,
                        Horizontal = !header && table.Snapshot.Columns[column].Kind == DataValueKind.Number ? CellHorizontalAlign.Right : null,
                        Top = grid,
                        Bottom = grid,
                        Left = grid,
                        Right = grid,
                    },
                });
            }
        return formats;
    }
}

public interface IDeckTableCellFormatting
{
    TableCellLook? LookOf(DataTableObject table, TableCellRange? cells);

    DataTableObject Apply(DataTableObject table, TableCellRange? cells, Func<TableCellLook, TableCellLook> change);

    DataTableObject Toggle(DataTableObject table, TableCellRange? cells, Func<TableCellLook, bool> read, Func<TableCellLook, bool, TableCellLook> write);
}

public sealed class DeckTableCellFormatting(IDeckTableBorders borders, IDeckTableLookSeed seed) : IDeckTableCellFormatting
{
    public TableCellLook? LookOf(DataTableObject table, TableCellRange? cells)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Snapshot.Columns.Count == 0) return null;
        var target = borders.Target(table, cells);
        return seed.Formats(table).FirstOrDefault(format => format.Row == target.FirstRow && format.Column == target.FirstColumn)?.Look
            ?? TableCellLook.Plain;
    }

    public DataTableObject Toggle(DataTableObject table, TableCellRange? cells, Func<TableCellLook, bool> read, Func<TableCellLook, bool, TableCellLook> write)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        var on = !(LookOf(table, cells) is { } look && read(look));
        return Apply(table, cells, current => write(current, on));
    }

    public DataTableObject Apply(DataTableObject table, TableCellRange? cells, Func<TableCellLook, TableCellLook> change)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(change);
        if (table.Snapshot.Columns.Count == 0) return table;
        var target = borders.Target(table, cells);
        var formats = seed.Formats(table).ToList();
        var positions = new Dictionary<(int Row, int Column), int>();
        for (var i = 0; i < formats.Count; i++) positions.TryAdd((formats[i].Row, formats[i].Column), i);
        for (var row = target.FirstRow; row <= target.LastRow; row++)
            for (var column = target.FirstColumn; column <= target.LastColumn; column++)
            {
                if (positions.TryGetValue((row, column), out var at))
                    formats[at] = formats[at] with { Look = change(formats[at].Look ?? TableCellLook.Plain) };
                else
                {
                    positions[(row, column)] = formats.Count;
                    formats.Add(new TableCellFormat(row, column, CellVerticalAlign.Top) { Look = change(TableCellLook.Plain) });
                }
            }
        return table with { CellFormats = formats, Style = table.Style with { OwnLook = true } };
    }
}

public interface IDeckTableCellEditing
{
    bool Available(DeckTarget? target);

    TableCellLook? Look(DeckTarget? target);

    void Apply(DeckTarget? target, Func<TableCellLook, TableCellLook> change);

    void Toggle(DeckTarget? target, Func<TableCellLook, bool> read, Func<TableCellLook, bool, TableCellLook> write);
}

public sealed class DeckTableCellEditing(IDeckTableCellFormatting formatting) : IDeckTableCellEditing
{
    public bool Available(DeckTarget? target) =>
        target is not null && !target.HasTextTarget() && target.Selected() is ObjectElement { Content: DataTableObject };

    public TableCellLook? Look(DeckTarget? target) =>
        Available(target) && target!.Selected() is ObjectElement { Content: DataTableObject table } element
            ? formatting.LookOf(table, Cells(target, element))
            : null;

    public void Apply(DeckTarget? target, Func<TableCellLook, TableCellLook> change) =>
        Edit(target, (table, cells) => formatting.Apply(table, cells, change));

    public void Toggle(DeckTarget? target, Func<TableCellLook, bool> read, Func<TableCellLook, bool, TableCellLook> write) =>
        Edit(target, (table, cells) => formatting.Toggle(table, cells, read, write));

    private void Edit(DeckTarget? target, Func<DataTableObject, TableCellRange?, DataTableObject> change)
    {
        if (!Available(target) || target!.CurrentSlide() is not { } slide
            || target.Selected() is not ObjectElement { Content: DataTableObject } element) return;
        var cells = Cells(target, element);
        target.Actions.EditTable(slide, element.Id, table => change(table, cells));
    }

    private TableCellRange? Cells(DeckTarget target, ObjectElement element) =>
        target.Options.TableCells is { } cells && cells.ElementId == element.Id ? cells.Range : null;
}
