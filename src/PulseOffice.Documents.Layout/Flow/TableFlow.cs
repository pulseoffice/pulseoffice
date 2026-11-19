using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.Layout.Flow;

internal enum SplitOutcome
{
    None,
    Finished,
    Continued,
}

internal sealed class TableFlow(PaginationRun run, ITableComposer tables, PageStart resume)
{
    private const double Epsilon = 0.001;

    private int _row = resume.Row;
    private int[]? _cells = resume.Cells.Count == 0 ? null : [.. resume.Cells.Items];
    private List<CarriedCell> _carry = [.. resume.Carried.Items];
    private bool _onlyHeaders;

    public int Row => _row;

    public void NewColumn() => _onlyHeaders = false;

    public PageStart Mark(PageStart start) => start with
    {
        Row = _row,
        Cells = _cells is null ? ValueList<int>.Empty : new ValueList<int>([.. _cells]),
        Carried = new ValueList<CarriedCell>([.. _carry]),
    };

    public bool Place(TableItem item)
    {
        var width = run.ColumnWidth;
        var placement = tables.Place(item, width, run.Geometry.Body.Height);
        if (_row == 0 && _cells is null && item.Story == StoryKind.Body && (item.Table.PageStyleName is not null || item.Table.PageBreakBefore))
        {
            var requested = item.Table.PageStyleName;
            if (!run.PageEmpty)
            {
                run.NewPage(requested);
                return true;
            }
            if (requested is not null && requested != run.Geometry.StyleName)
            {
                run.Reopen(requested, null);
                return true;
            }
        }
        if (_row == 0 && _cells is null && !run.ColumnEmpty)
        {
            run.Y += run.PreviousAfter;
            run.PreviousAfter = 0;
        }
        while (_row < placement.Rows.Count)
        {
            if (run.ColumnEmpty && _row > 0 && item.HeaderRows > 0 && _row >= item.HeaderRows)
            {
                var header = placement.Rows.Take(item.HeaderRows).Sum(row => row.Height);
                if (header < run.Available)
                {
                    Emit(placement, 0, item.HeaderRows, null, header, item);
                    _onlyHeaders = true;
                }
            }
            if (!run.ColumnEmpty && run.Available <= Epsilon)
            {
                run.Advance();
                return true;
            }
            var end = placement.GroupEnd(_row);
            var free = run.ColumnEmpty || _onlyHeaders;
            if (_cells is null)
            {
                var heights = Heights(placement, _row, end);
                var height = heights.Sum();
                var grouped = (end - _row > 1 || _carry.Count > 0) && GroupSplittable(placement, _row, end);
                if (height + RowNotes(placement, _row, end) <= run.Available + Epsilon || free && !grouped && !Splittable(placement, item, _row, end))
                {
                    var start = _row;
                    var overflow = EmitWith(placement, new TableEmission(_row, end, run.ColumnLeft, run.Y, item.Story, r => heights[r - start]), height, item);
                    if (Overflowed(placement, overflow, end)) return true;
                    _row = end;
                    continue;
                }
                if (grouped)
                {
                    if (!SplitGroup(placement, item, end, free, heights)) run.Advance();
                    return true;
                }
                var outcome = Splittable(placement, item, _row, end) ? Split(placement, item, new int[placement.Rows[_row].Cells.Count], free) : SplitOutcome.None;
                if (outcome == SplitOutcome.Finished) continue;
                if (outcome == SplitOutcome.None) run.Advance();
                return true;
            }
            var stretch = ContinuationEnd(placement);
            var continued = Continuation(placement, stretch);
            var rest = continued.Sum();
            if (rest <= run.Available + Epsilon)
            {
                var first = _row;
                var slices = placement.Rows[_row].Cells.Select((cell, c) => (c, new CellSlice(_cells[c], cell.Story.Atoms.Count, 0))).ToDictionary(pair => pair.c, pair => pair.Item2);
                var overflow = EmitWith(placement, new TableEmission(_row, stretch, run.ColumnLeft, run.Y, item.Story, r => continued[r - first])
                {
                    Slice = (r, c) => r == first && slices.TryGetValue(c, out var value) ? value : null,
                }, rest, item);
                if (Overflowed(placement, overflow, stretch)) return true;
                _cells = null;
                _row = stretch;
                continue;
            }
            var again = Split(placement, item, _cells, free);
            if (again == SplitOutcome.Finished) continue;
            if (again == SplitOutcome.None) run.Advance();
            return true;
        }
        _row = 0;
        _cells = null;
        _carry = [];
        run.FinishTable();
        return true;
    }

    private double RowNotes(TablePlacement placement, int row, int end)
    {
        var ids = placement.Rows.Skip(row).Take(end - row).SelectMany(value => value.Cells).SelectMany(cell => cell.Story.Atoms).SelectMany(atom => atom.Notes);
        return run.Footnotes.Cost(ids, run.Footnotes.PlacedIds(), 0, out _);
    }

    private bool GroupSplittable(TablePlacement placement, int row, int end)
    {
        for (var r = row; r < end; r++)
        {
            if (!placement.Table.Rows[r].Row.AllowBreakAcrossPages || placement.Table.Rows[r].Exact) return false;
        }
        return true;
    }

    private double[] Heights(TablePlacement placement, int row, int end)
    {
        var heights = new double[end - row];
        var carried = _carry.Where(carry => carry.Row <= row && carry.Row < placement.Rows.Count && carry.Cell < placement.Rows[carry.Row].Cells.Count).ToList();
        for (var r = row; r < end; r++)
        {
            var spanned = carried.Any(carry => carry.Row + placement.Rows[carry.Row].Cells[carry.Cell].Cell.RowSpan > r);
            heights[r - row] = spanned ? placement.Rows[r].Natural : placement.Rows[r].Height;
        }
        foreach (var carry in carried)
        {
            var cell = placement.Rows[carry.Row].Cells[carry.Cell];
            var spanEnd = carry.Row + cell.Cell.RowSpan;
            if (spanEnd > end) continue;
            var atoms = cell.Story.Atoms;
            var need = (carry.From < atoms.Count ? atoms[^1].Bottom - atoms[carry.From].Top : 0) + Padding(placement, cell);
            var sum = 0.0;
            for (var r = row; r < spanEnd; r++) sum += heights[r - row];
            if (need > sum) heights[spanEnd - 1 - row] += need - sum;
        }
        return heights;
    }

    private bool SplitGroup(TablePlacement placement, TableItem item, int end, bool force, double[] heights)
    {
        var k = _row;
        var used = 0.0;
        var first = _row;
        while (k < end && used + heights[k - first] + RowNotes(placement, _row, k + 1) <= run.Available + Epsilon)
        {
            used += heights[k - first];
            k++;
        }
        if (k == _row && end - _row == 1)
        {
            var inner = Split(placement, item, new int[placement.Rows[_row].Cells.Count], force || _carry.Count > 0);
            return inner != SplitOutcome.None;
        }
        var partial = k < end && run.Available - used > RowPadding(placement, k) + Epsilon && (k > _row || Starts(placement, k, run.Available));
        if (k == _row && !partial)
        {
            if (!force) return false;
            used = heights[0];
            k++;
            partial = k < end && run.Available - used > RowPadding(placement, k) + Epsilon;
        }
        var last = partial ? k + 1 : k;
        var natural = new double[last - _row];
        for (var r = _row; r < k; r++) natural[r - _row] = heights[r - first];
        if (!partial && k - _row > 0 && natural.Sum() < used) natural[k - 1 - _row] += used - natural.Sum();
        if (partial) natural[k - _row] = run.Available - used;
        var slices = new Dictionary<(int Row, int Cell), CellSlice>();
        var carried = new List<CarriedCell>();
        int[]? next = null;
        for (var r = _row; r < last; r++)
        {
            for (var c = 0; c < placement.Rows[r].Cells.Count; c++)
            {
                var cell = placement.Rows[r].Cells[c];
                var atoms = cell.Story.Atoms;
                if (partial && r == k)
                {
                    var own = 0;
                    while (own < atoms.Count && atoms[own].Bottom <= natural[k - _row] - Padding(placement, cell) + Epsilon) own++;
                    slices[(r, c)] = new CellSlice(0, own, 0);
                    next ??= new int[placement.Rows[k].Cells.Count];
                    next[c] = own;
                    if (cell.Cell.RowSpan > 1 && own < atoms.Count)
                    {
                        carried.Add(new CarriedCell(r, c, own, atoms.Count));
                        next[c] = atoms.Count;
                    }
                    continue;
                }
                if (r + cell.Cell.RowSpan <= k) continue;
                var room = natural.Skip(r - _row).Sum() - Padding(placement, cell);
                var to = 0;
                while (to < atoms.Count && atoms[to].Bottom <= room + Epsilon) to++;
                slices[(r, c)] = new CellSlice(0, to, 0);
                if (to < atoms.Count) carried.Add(new CarriedCell(r, c, to, atoms.Count));
            }
        }
        var total = natural.Sum();
        var overflow = EmitWith(placement, new TableEmission(_row, last, run.ColumnLeft, run.Y, item.Story, r => natural[r - first])
        {
            Slice = (r, c) => slices.TryGetValue((r, c), out var slice) ? slice : null,
            Sliced = partial,
        }, total, item);
        _carry.AddRange(carried);
        if (!partial && Overflowed(placement, overflow, k)) return true;
        _row = k;
        _cells = next;
        run.Advance();
        return true;
    }

    private bool Starts(TablePlacement placement, int row, double room) =>
        placement.Rows[row].Cells.Any(cell => cell.Story.Atoms.Count > 0 && cell.Story.Atoms[0].Bottom <= room - Padding(placement, cell) + Epsilon);

    private double Padding(TablePlacement placement, CellPlacement cell)
    {
        var insets = tables.Insets(placement.Table, cell.Cell);
        return insets.Top + insets.Bottom;
    }

    private double RowPadding(TablePlacement placement, int row) =>
        placement.Rows[row].Cells.Select(cell => Padding(placement, cell)).DefaultIfEmpty(2 * tables.Inset(placement.Table)).Max();

    private bool Splittable(TablePlacement placement, TableItem item, int row, int end) =>
        end - row == 1 && placement.Table.Rows[row].Row.AllowBreakAcrossPages && !placement.Table.Rows[row].Exact;

    private SplitOutcome Split(TablePlacement placement, TableItem item, int[] from, bool force)
    {
        var cells = placement.Rows[_row].Cells;
        var slices = new Dictionary<int, CellSlice>();
        var next = new int[cells.Count];
        var progress = false;
        var used = 0.0;
        for (var c = 0; c < cells.Count; c++)
        {
            var atoms = cells[c].Story.Atoms;
            var padding = Padding(placement, cells[c]);
            var available = run.Available - padding;
            var start = from[c];
            var origin = start == 0 ? 0 : start < atoms.Count ? atoms[start].Top : 0;
            var to = start;
            var placedNotes = run.Footnotes.PlacedIds();
            var noteCost = 0.0;
            while (to < atoms.Count)
            {
                var cost = run.Footnotes.Cost(atoms[to].Notes, placedNotes, noteCost, out var added);
                if (atoms[to].Bottom - origin + noteCost + cost > available + Epsilon) break;
                noteCost += cost;
                foreach (var id in added) placedNotes.Add(id);
                to++;
            }
            if (to == start && force && start < atoms.Count) to = start + 1;
            if (to > start)
            {
                progress = true;
                used = Math.Max(used, atoms[to - 1].Bottom - origin + padding);
            }
            slices[c] = new CellSlice(start, to, 0);
            next[c] = to;
        }
        var spanning = _carry.Any(carry => carry.Row <= _row && carry.Row + placement.Rows[carry.Row].Cells[carry.Cell].Cell.RowSpan > _row);
        var exhausted = force && cells.Select((cell, c) => from[c] >= cell.Story.Atoms.Count).All(done => done);
        if (!progress && !spanning && !exhausted) return SplitOutcome.None;
        var partHeight = spanning || exhausted ? Math.Max(Math.Max(used, RowPadding(placement, _row)), run.Available) : Math.Max(used, RowPadding(placement, _row));
        Emit(placement, _row, _row + 1, slices, partHeight, item);
        var finished = cells.Select((cell, c) => next[c] >= cell.Story.Atoms.Count).All(done => done) && !_carry.Any(carry => carry.Row < _row && carry.Row + placement.Rows[carry.Row].Cells[carry.Cell].Cell.RowSpan > _row);
        if (finished)
        {
            _cells = null;
            _row++;
            return SplitOutcome.Finished;
        }
        _cells = next;
        run.Advance();
        return SplitOutcome.Continued;
    }

    private void Emit(TablePlacement placement, int row, int end, IReadOnlyDictionary<int, CellSlice>? slices, double height, TableItem item) =>
        EmitWith(placement, new TableEmission(row, end, run.ColumnLeft, run.Y, item.Story, r => slices is null && end - row > 1 ? placement.Rows[r].Height : height)
        {
            Slice = (r, c) => slices is not null && slices.TryGetValue(c, out var value) ? value : null,
            Sliced = slices is not null,
        }, height, item);

    private IReadOnlyList<CarriedCell> Carry(TablePlacement placement, TableEmission emission, double limit, out double extra, out bool overflow)
    {
        extra = 0;
        overflow = false;
        var carried = new List<CarriedCell>();
        var next = new List<CarriedCell>();
        foreach (var carry in _carry)
        {
            if (carry.Row >= placement.Rows.Count || carry.Cell >= placement.Rows[carry.Row].Cells.Count) continue;
            var cell = placement.Rows[carry.Row].Cells[carry.Cell];
            var spanEnd = carry.Row + cell.Cell.RowSpan;
            if (spanEnd <= emission.Row) continue;
            if (emission.Row < carry.Row || emission.Row >= emission.RowEnd)
            {
                next.Add(carry);
                continue;
            }
            var last = Math.Min(spanEnd, emission.RowEnd);
            var room = -Padding(placement, cell);
            for (var r = emission.Row; r < last; r++) room += emission.RowHeight(r);
            var atoms = cell.Story.Atoms;
            var origin = carry.From < atoms.Count ? atoms[carry.From].Top : 0;
            var to = carry.From;
            while (to < atoms.Count && atoms[to].Bottom - origin <= room + Epsilon) to++;
            if (last == spanEnd && to < atoms.Count && !emission.Sliced)
            {
                var need = atoms[^1].Bottom - atoms[to].Top;
                if (need <= limit + Epsilon)
                {
                    extra = Math.Max(extra, need);
                    to = atoms.Count;
                }
                else
                {
                    var grown = to;
                    while (grown < atoms.Count && atoms[grown].Bottom - origin <= room + limit + Epsilon) grown++;
                    if (grown == carry.From && run.ColumnEmpty) grown++;
                    if (grown > to) extra = Math.Max(extra, atoms[grown - 1].Bottom - origin - room);
                    to = grown;
                    overflow = true;
                }
            }
            else if (to == carry.From && emission.Sliced && run.ColumnEmpty && to < atoms.Count)
            {
                to++;
            }
            carried.Add(carry with { To = to });
            if (to < atoms.Count) next.Add(carry with { From = to, To = atoms.Count });
        }
        _carry = next;
        return carried;
    }

    private bool EmitWith(TablePlacement placement, TableEmission emission, double height, TableItem item)
    {
        var carried = Carry(placement, emission, Math.Max(0, run.Available - height), out var extra, out var overflow);
        if (extra > 0)
        {
            var basis = emission.RowHeight;
            var lastRow = emission.RowEnd - 1;
            emission = emission with { RowHeight = r => basis(r) + (r == lastRow ? extra : 0) };
            height += extra;
        }
        var box = tables.Emit(placement, emission with { Carried = carried });
        if (run.Tables.Count > 0 && run.Tables[^1] is { } last && last.TableId == box.TableId && Math.Abs(last.Bounds.X - box.Bounds.X) < Epsilon && Math.Abs(last.Bounds.Bottom - box.Bounds.Y) < Epsilon)
        {
            run.Tables[^1] = new TableBox(box.TableId, last.Bounds with { Height = last.Bounds.Height + box.Bounds.Height }, [.. last.Cells, .. WithoutDrawnEdges(last.Cells, box.Cells)]);
        }
        else
        {
            run.Tables.Add(box);
        }
        foreach (var line in box.Lines)
        {
            run.Lines.Add(line);
            foreach (var note in line.Line.Notes) run.Footnotes.Reserve(note, run.Y + height);
        }
        run.Y += height;
        run.ColumnEmpty = false;
        _onlyHeaders = false;
        return overflow;
    }

    private IEnumerable<CellBox> WithoutDrawnEdges(IReadOnlyList<CellBox> drawn, IReadOnlyList<CellBox> cells)
    {
        var keys = drawn.SelectMany(cell => cell.Content.OfType<DecorationBox>()).Where(box => box.Kind == DecorationKind.CellEdge).Select(EdgeKey).ToHashSet();
        return cells.Select(cell => new CellBox(cell.TableId, cell.Row, cell.Column, cell.Bounds,
            [.. cell.Content.Where(box => box is not DecorationBox { Kind: DecorationKind.CellEdge } edge || !keys.Contains(EdgeKey(edge)))]));
    }

    private (long, long, long, long, uint, PulseOffice.Text.Rich.BorderLineStyle) EdgeKey(DecorationBox edge) =>
        (Snap(edge.Bounds.X), Snap(edge.Bounds.Y), Snap(edge.Bounds.Width), Snap(edge.Bounds.Height), edge.Color ?? 0, edge.LineStyle);

    private long Snap(double value) => (long)Math.Round(value * 1000);

    private bool Overflowed(TablePlacement placement, bool overflow, int end)
    {
        if (!overflow) return false;
        _row = end - 1;
        _cells = [.. placement.Rows[_row].Cells.Select(cell => cell.Story.Atoms.Count)];
        run.Advance();
        return true;
    }

    private int ContinuationEnd(TablePlacement placement)
    {
        var end = _row + 1;
        foreach (var carry in _carry)
        {
            if (carry.Row > _row || carry.Row >= placement.Rows.Count || carry.Cell >= placement.Rows[carry.Row].Cells.Count) continue;
            end = Math.Max(end, carry.Row + placement.Rows[carry.Row].Cells[carry.Cell].Cell.RowSpan);
        }
        return Math.Min(placement.Rows.Count, Math.Max(end, _row + 1));
    }

    private double[] Continuation(TablePlacement placement, int end)
    {
        var heights = new double[end - _row];
        var row = placement.Rows[_row];
        var cells = _cells!;
        var carried = _carry.Where(carry => carry.Row <= _row && carry.Row < placement.Rows.Count && carry.Cell < placement.Rows[carry.Row].Cells.Count).ToList();
        var covered = carried.Any(carry => carry.Row < _row && carry.Row + placement.Rows[carry.Row].Cells[carry.Cell].Cell.RowSpan > _row);
        heights[0] = Math.Max(row.Cells.Select((cell, c) => cell.Story.HeightFrom(cells[c]) + Padding(placement, cell)).DefaultIfEmpty(0).Max(), (covered ? 0 : row.Height - row.Natural) + RowPadding(placement, _row));
        for (var r = _row + 1; r < end; r++)
        {
            var spanned = carried.Any(carry => carry.Row + placement.Rows[carry.Row].Cells[carry.Cell].Cell.RowSpan > r);
            heights[r - _row] = spanned ? placement.Rows[r].Natural : placement.Rows[r].Height;
        }
        foreach (var carry in carried)
        {
            var spanEnd = carry.Row + placement.Rows[carry.Row].Cells[carry.Cell].Cell.RowSpan;
            if (spanEnd <= _row || spanEnd > end) continue;
            var carriedCell = placement.Rows[carry.Row].Cells[carry.Cell];
            var atoms = carriedCell.Story.Atoms;
            var need = (carry.From < atoms.Count ? atoms[^1].Bottom - atoms[carry.From].Top : 0) + Padding(placement, carriedCell);
            var sum = 0.0;
            for (var r = _row; r < spanEnd; r++) sum += heights[r - _row];
            if (need > sum) heights[spanEnd - 1 - _row] += need - sum;
        }
        return heights;
    }
}
