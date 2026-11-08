using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Flow;

public sealed record StoryAtom(double Top, double Height, IReadOnlyList<LayoutBox> Boxes, ParagraphEntry? Paragraph)
{
    public double Bottom => Top + Height;

    public RowBand? Band { get; init; }

    public IEnumerable<string> Notes => Boxes.OfType<LineBox>().SelectMany(line => line.Line.Notes);
}

public sealed class RowBands(TablePlacement placement, int row, IReadOnlyList<double> bottoms)
{
    public TablePlacement Placement { get; } = placement;

    public int Row { get; } = row;

    public IReadOnlyList<double> Bottoms { get; } = bottoms;

    public int Count => Bottoms.Count;

    public double Top(int band) => band == 0 ? 0 : Bottoms[band - 1];

    public CellSlice Slice(int cell, int from, int to)
    {
        var atoms = Placement.Rows[Row].Cells[cell].Story.Atoms.Count;
        return new CellSlice(Math.Min(from, atoms), to == Count - 1 ? atoms : Math.Min(to + 1, atoms), 0);
    }
}

public sealed record RowBand(RowBands Bands, int Index);

public sealed class ComposedStory(IReadOnlyList<StoryAtom> atoms, double height, double width)
{
    public IReadOnlyList<StoryAtom> Atoms { get; } = atoms;

    public double Height { get; } = height;

    public double Width { get; } = width;

    public double HeightFrom(int atom) => atom >= Atoms.Count ? 0 : Height - Atoms[atom].Top;
}

public readonly record struct StoryCursor(int Item, int Offset, int Line)
{
    public static readonly StoryCursor Start = new(0, 0, 0);
}

public interface IStoryComposer
{
    ComposedStory Compose(IReadOnlyList<FlowItem> items, double width);

    ComposedStory ComposeFrom(IReadOnlyList<FlowItem> items, double width, StoryCursor start, double limit, out StoryCursor next);

    IReadOnlyList<LayoutBox> Emit(ComposedStory story, int from, int to, double x, double y, double width, StoryKind kind, string? storyId);
}

public interface IParagraphDecorator
{
    IReadOnlyList<DecorationBox> Decorate(ParagraphEntry entry, IReadOnlyList<LineBox> lines, double left, double width);
}

public sealed record BorderStroke(LayoutRect Bounds, BorderLine Line);

public interface IBorderStrokes
{
    IReadOnlyList<BorderStroke> Lines(LayoutRect outer, BoxBorders borders, bool top = true, bool bottom = true);
}

public sealed class BorderStrokes : IBorderStrokes
{
    public IReadOnlyList<BorderStroke> Lines(LayoutRect outer, BoxBorders borders, bool top = true, bool bottom = true)
    {
        var strokes = new List<BorderStroke>();
        if (top && borders.Top is { Width: > 0 } upper) strokes.Add(new(new LayoutRect(outer.X, outer.Y, outer.Width, upper.Width), upper));
        if (bottom && borders.Bottom is { Width: > 0 } lower) strokes.Add(new(new LayoutRect(outer.X, outer.Bottom - lower.Width, outer.Width, lower.Width), lower));
        if (borders.Left is { Width: > 0 } start) strokes.Add(new(new LayoutRect(outer.X, outer.Y, start.Width, outer.Height), start));
        if (borders.Right is { Width: > 0 } end) strokes.Add(new(new LayoutRect(outer.Right - end.Width, outer.Y, end.Width, outer.Height), end));
        return strokes;
    }
}

public sealed class ParagraphDecorator(IBorderStrokes strokes) : IParagraphDecorator
{
    public IReadOnlyList<DecorationBox> Decorate(ParagraphEntry entry, IReadOnlyList<LineBox> lines, double left, double width)
    {
        if (entry.Decoration is not { } decoration || lines.Count == 0) return [];
        var first = lines[0];
        var last = lines[^1];
        var borders = decoration.Borders;
        var top = first.Top - (first.Line.IsFirst ? decoration.Inset(BorderSide.Top) : 0);
        var bottom = last.Top + last.Line.Height + (last.Line.IsLast ? decoration.Inset(BorderSide.Bottom) : 0);
        var x = left + entry.Frame.LeftEdge - decoration.Inset(BorderSide.Left);
        var w = width - entry.Frame.MarginLeft - entry.Frame.MarginRight + decoration.Inset(BorderSide.Left) + decoration.Inset(BorderSide.Right);
        var rect = new LayoutRect(x, top, Math.Max(0, w), Math.Max(0, bottom - top));
        var boxes = new List<DecorationBox>();
        if (decoration.Background is not null) boxes.Add(new DecorationBox(DecorationKind.ParagraphBackground, rect, decoration.Background, 0) { Owner = entry.Block.Id });
        foreach (var line in strokes.Lines(rect, borders, first.Line.IsFirst, last.Line.IsLast))
            boxes.Add(new DecorationBox(DecorationKind.ParagraphBorder, line.Bounds, line.Line.Color, line.Line.Width) { Owner = entry.Block.Id, LineStyle = line.Line.Style });
        return boxes;
    }
}

public sealed record CellPlacement(TableCellItem Cell, int Row, double Left, double Width, ComposedStory Story)
{
    public ComposedStory? Lying { get; init; }
}

public sealed class RowPlacement(int index, IReadOnlyList<CellPlacement> cells, double height)
{
    public int Index { get; } = index;

    public IReadOnlyList<CellPlacement> Cells { get; } = cells;

    public double Height { get; set; } = height;

    public double Natural { get; } = height;
}

public sealed class TablePlacement(TableItem table, IReadOnlyList<RowPlacement> rows, double width, double offset = 0)
{
    public double Offset { get; } = offset;

    public TableItem Table { get; } = table;

    public IReadOnlyList<RowPlacement> Rows { get; } = rows;

    public double Width { get; } = width;

    public int GroupEnd(int row)
    {
        var end = row + 1;
        for (var r = row; r < end && r < Rows.Count; r++)
        {
            foreach (var cell in Rows[r].Cells) end = Math.Max(end, Math.Min(Rows.Count, r + cell.Cell.RowSpan));
        }
        return end;
    }
}

public sealed record CellSlice(int From, int To, double Offset);

public sealed record TableEmission(int Row, int RowEnd, double X, double Y, StoryKind Story, Func<int, double> RowHeight)
{
    public Func<int, int, CellSlice?> Slice { get; init; } = (_, _) => null;

    public IReadOnlyList<CarriedCell> Carried { get; init; } = [];

    public bool Sliced { get; init; }
}

public interface ITableComposer
{
    TablePlacement Place(TableItem table, double available);

    TablePlacement Place(TableItem table, double available, double turnedLimit);

    double Inset(TableItem table);

    double HorizontalInset(TableItem table);

    CellPadding Insets(TableItem table, TableCellItem cell);

    TableBox Emit(TablePlacement placement, int row, int rowEnd, IReadOnlyDictionary<int, CellSlice>? slices, double rowHeight, double x, double y, StoryKind story);

    TableBox Emit(TablePlacement placement, TableEmission emission);
}

public sealed class BlockStacker(IParagraphDecorator decorator) : IStoryComposer, ITableComposer
{
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TableItem, Dictionary<(double Available, double Limit), TablePlacement>> _tables = new();

    public ComposedStory Compose(IReadOnlyList<FlowItem> items, double width) => Compose(items, width, LongestTurnedLine);

    private ComposedStory Compose(IReadOnlyList<FlowItem> items, double width, double turnedLimit)
    {
        var atoms = new List<StoryAtom>();
        var y = 0.0;
        var previousAfter = 0.0;
        var first = true;
        foreach (var item in items)
        {
            if (item is ParagraphEntry entry)
            {
                y += first ? entry.Flow.SpaceBefore : previousAfter + entry.Flow.SpaceBefore;
                foreach (var line in entry.Lines(width))
                {
                    atoms.Add(new StoryAtom(y, line.Height, [new LineBox(line, 0, y)], entry));
                    y += line.Height;
                }
                previousAfter = entry.Flow.SpaceAfter;
                first = false;
            }
            else if (item is TableItem table)
            {
                y += previousAfter;
                var placement = Place(table, width, turnedLimit);
                foreach (var atom in RowAtoms(placement, y))
                {
                    atoms.Add(atom);
                    y = atom.Bottom;
                }
                previousAfter = 0;
                first = false;
            }
        }
        return new ComposedStory(atoms, y + previousAfter, width);
    }

    public ComposedStory ComposeFrom(IReadOnlyList<FlowItem> items, double width, StoryCursor start, double limit, out StoryCursor next)
    {
        var atoms = new List<StoryAtom>();
        var y = 0.0;
        var previousAfter = 0.0;
        next = new StoryCursor(items.Count, 0, 0);
        for (var index = start.Item; index < items.Count; index++)
        {
            var resumed = index == start.Item && (start.Offset > 0 || start.Line > 0);
            if (items[index] is ParagraphEntry entry)
            {
                var gap = resumed ? 0 : atoms.Count == 0 ? entry.Flow.SpaceBefore : previousAfter + entry.Flow.SpaceBefore;
                var lines = resumed ? entry.LinesFrom(width, start.Offset, start.Line) : entry.Lines(width);
                for (var i = 0; i < lines.Count; i++)
                {
                    var line = lines[i];
                    var top = y + (i == 0 ? gap : 0);
                    if (top + line.Height > limit + 0.01 && atoms.Count > 0)
                    {
                        next = new StoryCursor(index, i > 0 ? lines[i - 1].Next : resumed ? start.Offset : 0, line.Index);
                        return new ComposedStory(atoms, y, width);
                    }
                    atoms.Add(new StoryAtom(top, line.Height, [new LineBox(line, 0, top)], entry));
                    y = top + line.Height;
                }
                previousAfter = entry.Flow.SpaceAfter;
            }
            else if (items[index] is TableItem table)
            {
                var placement = Place(table, width, limit);
                var height = placement.Rows.Sum(row => row.Height);
                var top = y + previousAfter;
                if (top + height > limit + 0.01 && atoms.Count > 0)
                {
                    next = new StoryCursor(index, 0, 0);
                    return new ComposedStory(atoms, y, width);
                }
                foreach (var atom in RowAtoms(placement, top))
                {
                    atoms.Add(atom);
                    y = atom.Bottom;
                }
                y = Math.Max(y, top);
                previousAfter = 0;
            }
        }
        return new ComposedStory(atoms, y + previousAfter, width);
    }

    private IEnumerable<StoryAtom> RowAtoms(TablePlacement placement, double top)
    {
        var row = 0;
        while (row < placement.Rows.Count)
        {
            var end = placement.GroupEnd(row);
            var height = 0.0;
            for (var r = row; r < end; r++) height += placement.Rows[r].Height;
            var first = row;
            if (Banded(placement, row, end) is { } bands)
            {
                for (var band = 0; band < bands.Count; band++)
                {
                    var bandTop = top + bands.Top(band);
                    yield return new StoryAtom(bandTop, bands.Bottoms[band] - bands.Top(band), [BandBox(bands, band, band, 0, bandTop, StoryKind.Cell)], null) { Band = new RowBand(bands, band) };
                }
            }
            else
            {
                yield return new StoryAtom(top, height, [Emit(placement, new TableEmission(first, end, 0, top, StoryKind.Cell, r => placement.Rows[r].Height))], null);
            }
            top += height;
            row = end;
        }
    }

    private RowBands? Banded(TablePlacement placement, int row, int end)
    {
        var source = placement.Table.Rows[row];
        if (end - row != 1 || !source.Row.AllowBreakAcrossPages || source.Exact) return null;
        var cells = placement.Rows[row].Cells;
        if (cells.Count == 0 || cells.Any(cell => cell.Lying is not null || cell.Cell.RowSpan != 1)) return null;
        var count = cells.Max(cell => cell.Story.Atoms.Count);
        if (count < 2) return null;
        var height = placement.Rows[row].Height;
        var bottoms = new List<double>();
        var previous = 0.0;
        for (var band = 0; band < count - 1; band++)
        {
            var bottom = previous;
            foreach (var cell in cells)
            {
                var atoms = cell.Story.Atoms;
                if (atoms.Count == 0) continue;
                bottom = Math.Max(bottom, Insets(placement.Table, cell.Cell).Top + atoms[Math.Min(band, atoms.Count - 1)].Bottom);
            }
            previous = Math.Min(height, bottom);
            bottoms.Add(previous);
        }
        bottoms.Add(height);
        return new RowBands(placement, row, bottoms);
    }

    private TableBox BandBox(RowBands bands, int from, int to, double x, double y, StoryKind kind)
    {
        var placement = bands.Placement;
        if (from == 0 && to == bands.Count - 1)
            return Emit(placement, new TableEmission(bands.Row, bands.Row + 1, x, y, kind, r => placement.Rows[r].Height));
        var slices = new Dictionary<int, CellSlice>();
        for (var c = 0; c < placement.Rows[bands.Row].Cells.Count; c++) slices[c] = bands.Slice(c, from, to);
        return Emit(placement, bands.Row, bands.Row + 1, slices, bands.Bottoms[to] - bands.Top(from), x, y, kind);
    }

    public IReadOnlyList<LayoutBox> Emit(ComposedStory story, int from, int to, double x, double y, double width, StoryKind kind, string? storyId)
    {
        var boxes = new List<LayoutBox>();
        if (from >= to || from >= story.Atoms.Count) return boxes;
        var origin = story.Atoms[from].Top;
        var dy = y - origin;
        var lines = new List<(ParagraphEntry Entry, LineBox Line)>();
        for (var i = from; i < to && i < story.Atoms.Count; i++)
        {
            var atom = story.Atoms[i];
            if (atom.Band is { } band)
            {
                var last = i;
                while (last + 1 < to && last + 1 < story.Atoms.Count && story.Atoms[last + 1].Band is { } next && next.Bands == band.Bands) last++;
                boxes.Add(BandBox(band.Bands, band.Index, story.Atoms[last].Band!.Index, x, atom.Top + dy, StoryKind.Cell));
                i = last;
                continue;
            }
            foreach (var box in atom.Boxes)
            {
                if (box is LineBox line)
                {
                    var moved = line.MovedLine(x, dy).InStory(kind, storyId);
                    boxes.Add(moved);
                    if (atom.Paragraph is not null) lines.Add((atom.Paragraph, moved));
                }
                else
                {
                    boxes.Add(box.Moved(x, dy));
                }
            }
        }
        foreach (var group in lines.GroupBy(pair => pair.Entry))
        {
            boxes.InsertRange(0, decorator.Decorate(group.Key, [.. group.Select(pair => pair.Line)], x, width));
        }
        return boxes;
    }

    public double Inset(TableItem table) => table.Style.VerticalPadding + table.Style.BorderWidth / 2;

    public double HorizontalInset(TableItem table) => table.Style.Padding + table.Style.BorderWidth / 2;

    public CellPadding Insets(TableItem table, TableCellItem cell) => cell.Cell.Padding is { } own
        ? new CellPadding(own.Top + table.Style.BorderWidth / 2, own.Bottom + table.Style.BorderWidth / 2, own.Left + table.Style.BorderWidth / 2, own.Right + table.Style.BorderWidth / 2)
        : new CellPadding(Inset(table), Inset(table), HorizontalInset(table), HorizontalInset(table));

    public TablePlacement Place(TableItem table, double available) => Place(table, available, LongestTurnedLine);

    public TablePlacement Place(TableItem table, double available, double turnedLimit)
    {
        var limit = Math.Clamp(double.IsFinite(turnedLimit) ? turnedLimit : LongestTurnedLine, 1, LongestTurnedLine);
        var byWidth = _tables.GetValue(table, _ => []);
        if (byWidth.TryGetValue((available, limit), out var cached)) return cached;
        var rows = new List<RowPlacement>();
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var cells = new List<CellPlacement>();
            var height = 0.0;
            foreach (var cell in row.Cells)
            {
                if (cell.Cell.Covered) continue;
                var left = table.ColumnLeft(cell.Column, available);
                var width = table.ColumnWidth(cell.Column, cell.ColumnSpan, available);
                var insets = Insets(table, cell);
                var inner = Math.Max(1, limit - insets.Top - insets.Bottom);
                var placed = cell.Cell.TextDirection == CellTextDirection.Horizontal
                    ? new CellPlacement(cell, r, left, width, Compose(cell.Items, Math.Max(1, width - insets.Left - insets.Right), inner))
                    : Standing(cell, r, left, width, TurnedLength(cell, row.Height is { } given && cell.RowSpan == 1 ? given - insets.Top - insets.Bottom : null, row.Exact, inner), inner);
                cells.Add(placed);
                if (cell.RowSpan == 1) height = Math.Max(height, placed.Story.Height + insets.Top + insets.Bottom);
            }
            if (row.Height is { } fixedHeight) height = row.Exact ? fixedHeight : Math.Max(height, fixedHeight);
            rows.Add(new RowPlacement(r, cells, height));
        }
        foreach (var row in rows)
        {
            foreach (var cell in row.Cells.Where(cell => cell.Cell.RowSpan > 1))
            {
                var end = Math.Min(rows.Count, row.Index + cell.Cell.RowSpan);
                var spanned = rows.Skip(row.Index).Take(end - row.Index).Sum(item => item.Height);
                var insets = Insets(table, cell.Cell);
                var need = cell.Story.Height + insets.Top + insets.Bottom;
                if (need > spanned) rows[end - 1].Height += need - spanned;
            }
        }
        var placement = new TablePlacement(table, rows, table.Width(available), table.Offset(available));
        byWidth[(available, limit)] = placement;
        return placement;
    }

    private CellPlacement Standing(TableCellItem cell, int row, double left, double width, double length, double turnedLimit)
    {
        var lying = Compose(cell.Items, length, turnedLimit);
        var lines = lying.Atoms.SelectMany(atom => atom.Boxes).ToList();
        var standing = new ComposedStory([new StoryAtom(0, length, lines, null)], length, Math.Max(1, lying.Height));
        return new CellPlacement(cell, row, left, width, standing) { Lying = lying };
    }

    private double TurnedLength(TableCellItem cell, double? given, bool exact, double limit)
    {
        if (given is { } inner && exact) return Math.Max(1, inner);
        var natural = Math.Min(limit, Natural(Compose(cell.Items, LongestTurnedLine)));
        return Math.Max(natural, Math.Min(limit, Math.Max(1, given ?? 0)));
    }

    private const double LongestTurnedLine = 7920;

    private double Natural(ComposedStory story)
    {
        var extent = 0.0;
        foreach (var atom in story.Atoms)
        {
            var margins = atom.Paragraph is { } entry ? entry.Frame.MarginLeft + entry.Frame.MarginRight + Math.Max(0, entry.Frame.FirstLineIndent) : 0;
            foreach (var box in atom.Boxes)
            {
                var length = box switch
                {
                    LineBox line => line.Line.Runs.Sum(run => run.Width) + margins,
                    TableBox table => table.Bounds.Width,
                    _ => 0,
                };
                extent = Math.Max(extent, length);
            }
        }
        return Math.Max(1, Math.Min(LongestTurnedLine, Math.Ceiling(extent + 0.5)));
    }

    public TableBox Emit(TablePlacement placement, int row, int rowEnd, IReadOnlyDictionary<int, CellSlice>? slices, double rowHeight, double x, double y, StoryKind story) =>
        Emit(placement, new TableEmission(row, rowEnd, x, y, story, r => slices is null && rowEnd - row > 1 ? placement.Rows[r].Height : rowHeight)
        {
            Slice = (r, c) => slices is not null && slices.TryGetValue(c, out var value) ? value : null,
            Sliced = slices is not null,
        });

    public TableBox Emit(TablePlacement placement, TableEmission emission)
    {
        var table = placement.Table;
        var cells = new List<CellBox>();
        var x = emission.X + placement.Offset;
        var top = emission.Y;
        foreach (var carried in emission.Carried)
        {
            var cell = placement.Rows[carried.Row].Cells[carried.Cell];
            var spanEnd = Math.Min(emission.RowEnd, carried.Row + cell.Cell.RowSpan);
            var height = 0.0;
            for (var r = emission.Row; r < spanEnd; r++) height += emission.RowHeight(r);
            cells.Add(Cell(placement, cell, carried.Row, new CellSlice(carried.From, carried.To, 0), x, top, height));
        }
        for (var r = emission.Row; r < emission.RowEnd; r++)
        {
            var heightOfRow = emission.RowHeight(r);
            for (var c = 0; c < placement.Rows[r].Cells.Count; c++)
            {
                if (Carried(emission, r, c)) continue;
                var cell = placement.Rows[r].Cells[c];
                var spanEnd = Math.Min(emission.RowEnd, r + cell.Cell.RowSpan);
                var height = 0.0;
                for (var s = r; s < spanEnd; s++) height += emission.RowHeight(s);
                var slice = emission.Slice(r, c) ?? new CellSlice(0, cell.Story.Atoms.Count, 0);
                cells.Add(Cell(placement, cell, r, slice, x, top, height));
            }
            top += heightOfRow;
        }
        return new TableBox(table.Table.Id, new LayoutRect(x, emission.Y, placement.Width, top - emission.Y), cells);
    }

    private bool Carried(TableEmission emission, int row, int cell)
    {
        foreach (var carried in emission.Carried)
        {
            if (carried.Row == row && carried.Cell == cell) return true;
        }
        return false;
    }

    private CellBox Cell(TablePlacement placement, CellPlacement cell, int r, CellSlice slice, double x, double top, double height)
    {
        var table = placement.Table;
        var insets = Insets(table, cell.Cell);
        var contentHeight = slice.To > slice.From ? cell.Story.Atoms[slice.To - 1].Bottom - cell.Story.Atoms[slice.From].Top : 0;
        var free = height - insets.Top - insets.Bottom - contentHeight;
        var shift = cell.Cell.Cell.Covered ? 0 : cell.Cell.Cell.VerticalAlign switch
        {
            CellVerticalAlign.Middle => Math.Max(0, free / 2),
            CellVerticalAlign.Bottom => Math.Max(0, free),
            _ => 0,
        };
        var firstTop = slice.From < cell.Story.Atoms.Count ? cell.Story.Atoms[slice.From].Top : 0;
        var startY = top + insets.Top + shift + (slice.From == 0 ? firstTop : 0);
        var content = new List<LayoutBox>();
        var rect = new LayoutRect(x + cell.Left, top, cell.Width, height);
        var look = cell.Cell.Appearance;
        var background = look?.Background ?? cell.Cell.Cell.Background;
        if (background is not null) content.Add(new DecorationBox(DecorationKind.CellBackground, rect, background, 0) { Owner = table.Table.Id });
        if (look is null)
        {
            if (table.Style.BorderWidth > 0) content.Add(new DecorationBox(DecorationKind.CellBorder, rect, table.Style.BorderColor, table.Style.BorderWidth) { Owner = table.Table.Id });
        }
        else
        {
            content.AddRange(Edges(look.Borders, rect, table.Table.Id));
        }
        if (cell.Lying is { } lying)
        {
            if (slice.From == 0 && slice.To > 0) content.Add(Turned(cell, lying, new LayoutRect(x + cell.Left + insets.Left, top + insets.Top, cell.Width - insets.Left - insets.Right, height - insets.Top - insets.Bottom), table.Table.Id));
        }
        else
        {
            content.AddRange(Emit(cell.Story, slice.From, slice.To, x + cell.Left + insets.Left, startY, cell.Width - insets.Left - insets.Right, StoryKind.Cell, table.Table.Id));
        }
        return new CellBox(table.Table.Id, r, cell.Cell.Column, rect, content);
    }

    private TurnedBox Turned(CellPlacement cell, ComposedStory lying, LayoutRect area, string owner)
    {
        var length = Math.Max(0, area.Height);
        var thickness = Math.Max(0, area.Width);
        var frame = new LayoutRect(area.X + (thickness - length) / 2, area.Y + (length - thickness) / 2, length, thickness);
        var free = thickness - lying.Height;
        var shift = cell.Cell.Cell.VerticalAlign switch
        {
            CellVerticalAlign.Middle => Math.Max(0, free / 2),
            CellVerticalAlign.Bottom => Math.Max(0, free),
            _ => 0,
        };
        var firstTop = lying.Atoms.Count > 0 ? lying.Atoms[0].Top : 0;
        var degrees = cell.Cell.Cell.TextDirection == CellTextDirection.BottomToTop ? 270 : 90;
        return new TurnedBox(frame, degrees, Emit(lying, 0, lying.Atoms.Count, frame.X, frame.Y + shift + firstTop, length, StoryKind.Cell, owner));
    }

    private IEnumerable<DecorationBox> Edges(PulseOffice.Text.Rich.BoxBorders borders, LayoutRect rect, string owner)
    {
        if (borders.IsUniform && borders.Top is { Style: PulseOffice.Text.Rich.BorderLineStyle.Solid } line)
        {
            yield return new DecorationBox(DecorationKind.CellBorder, rect, line.Color, line.Width) { Owner = owner };
            yield break;
        }
        if (borders.Top is { } top) yield return Edge(new LayoutRect(rect.X, rect.Y - top.Width / 2, rect.Width, top.Width), top, owner);
        if (borders.Bottom is { } bottom) yield return Edge(new LayoutRect(rect.X, rect.Bottom - bottom.Width / 2, rect.Width, bottom.Width), bottom, owner);
        if (borders.Left is { } left) yield return Edge(new LayoutRect(rect.X - left.Width / 2, rect.Y, left.Width, rect.Height), left, owner);
        if (borders.Right is { } right) yield return Edge(new LayoutRect(rect.Right - right.Width / 2, rect.Y, right.Width, rect.Height), right, owner);
    }

    private DecorationBox Edge(LayoutRect bounds, PulseOffice.Text.Rich.BorderLine line, string owner) =>
        new(DecorationKind.CellEdge, bounds, line.Color, line.Width) { Owner = owner, LineStyle = line.Style };
}
