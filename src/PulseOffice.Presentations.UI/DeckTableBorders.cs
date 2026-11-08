using PulseOffice.Document.Borders;
using PulseOffice.Document.Embedding;
using PulseOffice.Document.Tables;

namespace PulseOffice.Presentations.UI;

public sealed record DeckTableBorderTarget(int FirstRow, int FirstColumn, int LastRow, int LastColumn);

public interface IDeckTableBorders
{
    DeckTableBorderTarget Target(DataTableObject table, TableCellRange? cells);

    BorderFrameScope Scope(DeckTableBorderTarget target);

    BorderFrame FrameOf(DataTableObject table, DeckTableBorderTarget target);

    DataTableObject Apply(DataTableObject table, DeckTableBorderTarget target, BorderFrame before, BorderFrame after);
}

public sealed class DeckTableBorders(IBorderFrameResolver resolver, IDeckTableLookSeed seed) : IDeckTableBorders
{
    private const string Black = "#000000";

    public DeckTableBorderTarget Target(DataTableObject table, TableCellRange? cells)
    {
        ArgumentNullException.ThrowIfNull(table);
        var top = table.Style.HeaderRow ? TableCellFormat.HeaderRow : 0;
        var bottom = table.Snapshot.Rows.Count - 1;
        var right = table.Snapshot.Columns.Count - 1;
        if (cells is not { } range) return new DeckTableBorderTarget(top, 0, Math.Max(top, bottom), Math.Max(0, right));
        return new DeckTableBorderTarget(
            Math.Clamp(range.Top, top, Math.Max(top, bottom)),
            Math.Clamp(range.Left, 0, Math.Max(0, right)),
            Math.Clamp(range.Bottom, top, Math.Max(top, bottom)),
            Math.Clamp(range.Right, 0, Math.Max(0, right)));
    }

    public BorderFrameScope Scope(DeckTableBorderTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return new BorderFrameScope(target.LastRow > target.FirstRow, target.LastColumn > target.FirstColumn, false)
        {
            Dashes = [BorderDash.Solid],
        };
    }

    public BorderFrame FrameOf(DataTableObject table, DeckTableBorderTarget target)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(target);
        var index = new TableCellIndex(table with { CellFormats = seed.Formats(table) });
        var first = index.Format(target.FirstRow, target.FirstColumn)?.Look;
        var last = index.Format(target.LastRow, target.LastColumn)?.Look;
        var frame = BorderFrame.Empty
            .With(BorderPosition.Top, Stroke(first?.Top))
            .With(BorderPosition.Left, Stroke(first?.Left))
            .With(BorderPosition.Bottom, Stroke(last?.Bottom))
            .With(BorderPosition.Right, Stroke(last?.Right));
        if (target.LastRow > target.FirstRow) frame = frame.With(BorderPosition.InsideHorizontal, Stroke(first?.Bottom));
        if (target.LastColumn > target.FirstColumn) frame = frame.With(BorderPosition.InsideVertical, Stroke(first?.Right));
        return frame;
    }

    public DataTableObject Apply(DataTableObject table, DeckTableBorderTarget target, BorderFrame before, BorderFrame after)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(target);
        var formats = seed.Formats(table).ToList();
        var positions = new Dictionary<(int Row, int Column), int>();
        for (var i = 0; i < formats.Count; i++) positions.TryAdd((formats[i].Row, formats[i].Column), i);
        for (var row = target.FirstRow; row <= target.LastRow; row++)
            for (var column = target.FirstColumn; column <= target.LastColumn; column++)
            {
                var edits = resolver.Resolve(before, after, resolver.Place(row, column, target.FirstRow, target.FirstColumn, target.LastRow, target.LastColumn));
                if (edits.IsEmpty) continue;
                var index = positions.GetValueOrDefault((row, column), -1);
                var format = index >= 0 ? formats[index] : new TableCellFormat(row, column, CellVerticalAlign.Center);
                var look = format.Look ?? TableCellLook.Plain;
                var updated = format with
                {
                    Look = look with
                    {
                        Top = edits.Top.Apply(look.Top, Line),
                        Bottom = edits.Bottom.Apply(look.Bottom, Line),
                        Left = edits.Left.Apply(look.Left, Line),
                        Right = edits.Right.Apply(look.Right, Line),
                    },
                };
                if (index >= 0) formats[index] = updated;
                else
                {
                    positions[(row, column)] = formats.Count;
                    formats.Add(updated);
                }
            }
        return table with { CellFormats = formats, Style = table.Style with { OwnLook = true } };
    }

    private BorderStroke? Stroke(TableBorderLine? line) =>
        line is null || line.WidthPoints <= 0 ? null : new BorderStroke(BorderDash.Solid, line.WidthPoints, line.ColorHex);

    private TableBorderLine? Line(BorderStroke? stroke) =>
        stroke is null ? null : new TableBorderLine(string.IsNullOrEmpty(stroke.ColorHex) ? Black : stroke.ColorHex, stroke.WidthPoints);
}
