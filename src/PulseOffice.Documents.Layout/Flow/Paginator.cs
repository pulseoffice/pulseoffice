using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;

namespace PulseOffice.Documents.Layout.Flow;

public interface IConvergenceProbe
{
    IReadOnlyList<PageBox>? Remainder(int pageIndex, PageStart start);
}

public sealed record PaginationRequest(
    IReadOnlyList<FlowItem> Flow,
    Func<string, PageSide, PageGeometry> Geometry,
    string InitialStyle,
    IReadOnlyList<PageBox> Prefix,
    PageStart Resume,
    IConvergenceProbe? Probe)
{
    public Func<string, double, ComposedStory?> Note { get; init; } = (_, _) => null;

    public Func<string, AnchoredObject?> Object { get; init; } = _ => null;

    public Func<int, IReadOnlyList<AnchoredObject>> PageObjects { get; init; } = _ => [];

    public Func<ParagraphEntry, string, ParagraphEntry> PerPage { get; init; } = (entry, _) => entry;

    public string PageCount { get; init; } = "00";

    public Func<string, int, string> PageNumber { get; init; } = (_, number) => number.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public Func<string, double> FrameInset { get; init; } = _ => 0;

    public Func<string, double> FrameAlignment { get; init; } = _ => 0;

    public Func<string, ComposedStory?> Frame { get; init; } = _ => null;

    public Func<string, IReadOnlyList<MemberFrame>> MemberFrames { get; init; } = _ => [];
}

public sealed record MemberFrame(string MemberId, double X, double Y, double Width, double Height, double Inset, double Alignment, ComposedStory Story);

public interface IPaginator
{
    IReadOnlyList<PageBox> Paginate(PaginationRequest request);
}

public sealed class Paginator(ITableComposer tables, IStoryComposer stories, IParagraphDecorator decorator, IObjectPlacement objects, IWrapSegments wrap, IColumnBalancer balancer, IPageBackdrops backdrops, PageFlowOptions options, IObjectOutlines outlines) : IPaginator
{
    private IObjectOutlines Outlines { get; } = outlines;

    private IPageBackdrops Backdrops { get; } = backdrops;

    private ITableComposer Tables { get; } = tables;

    private IStoryComposer Stories { get; } = stories;

    private IParagraphDecorator Decorator { get; } = decorator;

    private IObjectPlacement Objects { get; } = objects;

    private IWrapSegments Wrap { get; } = wrap;

    private IColumnBalancer Balancer { get; } = balancer;

    private PageFlowOptions Options { get; } = options;

    public IReadOnlyList<PageBox> Paginate(PaginationRequest request) => new PaginationRun(this, request).Run();

    private sealed record PlacedNote(string NoteId, ComposedStory Story, int From, int To, int Area, double Left, double Width);

    private sealed record DynamicRow(int Lines, double Y, int Line, int Offset);

    private sealed record Obstacle(WrapObstacle Shape, LayoutRect Area)
    {
        public LayoutRect Rect => Shape.Rect;

        public WrapMode Mode => Shape.Mode;

        public string? OnlyParagraph { get; init; }

        public Obstacle For(string paragraphId) => OnlyParagraph is { } only && only != paragraphId && Mode != WrapMode.Through ? this with { Shape = Shape with { Mode = WrapMode.None, Outline = null } } : this;
    }

    private enum SplitOutcome
    {
        None,
        Finished,
        Continued,
    }

    private sealed class PaginationRun : IFlowItemVisitor<bool>
    {
        private const double Epsilon = 0.001;

        private readonly Paginator _owner;
        private readonly PaginationRequest _request;
        private readonly List<PageBox> _pages;
        private readonly List<LineBox> _lines = [];
        private readonly List<TableBox> _tables = [];
        private readonly List<AreaBox> _columnAreas = [];
        private readonly List<ObjectBox> _objects = [];
        private readonly List<DecorationBox> _decorations = [];
        private readonly List<PlacedNote> _notes = [];
        private readonly List<PendingNote> _pending = [];
        private readonly List<Obstacle> _obstacles = [];
        private readonly HashSet<string> _placedObjects = new(StringComparer.Ordinal);
        private readonly Stack<ColumnLayout> _sections = new();
        private readonly List<(ParagraphEntry Entry, LineBox Line, double Left, double Width)> _decorated = [];
        private PageGeometry _geometry = null!;
        private PageStart _start;
        private (string Style, int? Restart)? _deferred;
        private ColumnLayout _region = ColumnLayout.Single;
        private double _regionTop;
        private double _balanceLimit = double.PositiveInfinity;
        private double[] _columnBottoms = [];
        private int _column;
        private double _y;
        private double _noteHeight;
        private double[] _columnNotes = [0];
        private double _previousAfter;
        private string? _previousStyle;
        private bool _columnEmpty = true;
        private bool _onlyHeaders;
        private bool _converged;
        private int _flowIndex;
        private int _line;
        private int _offset;
        private int _row;
        private int[]? _cells;
        private List<CarriedCell> _carry = [];

        public PaginationRun(Paginator owner, PaginationRequest request)
        {
            _owner = owner;
            _request = request;
            _pages = [.. request.Prefix];
            var resume = request.Resume;
            _flowIndex = resume.FlowIndex;
            _line = resume.Line;
            _offset = resume.Offset;
            _row = resume.Row;
            _cells = resume.Cells.Count == 0 ? null : [.. resume.Cells.Items];
            _carry = [.. resume.Carried.Items];
            for (var i = 0; i < Math.Min(_flowIndex, request.Flow.Count); i++)
            {
                if (request.Flow[i] is SectionStartItem start) _sections.Push(start.Columns);
                else if (request.Flow[i] is SectionEndItem && _sections.Count > 0) _sections.Pop();
            }
            _start = resume;
            Open(resume.Style, resume.Pending.Items);
        }

        private bool PageEmpty => _lines.Count == 0 && _tables.Count == 0;

        private double ColumnWidth => _region.Width(_geometry.Body.Width);

        private double ColumnLeft => _region.Left(_geometry.Body.X, _geometry.Body.Width, _column);

        private double PageBottom => _geometry.Body.Bottom - _noteHeight - (PerColumn ? _columnNotes[_column] : 0);

        private bool PerColumn => _region.Count > 1 && (_sections.Count == 0 || !_region.Balanced);

        private int NoteArea => PerColumn ? _column : -1;

        private double NoteWidth => PerColumn ? ColumnWidth : _geometry.Body.Width;

        private double Bottom => _column < _region.Count - 1 ? Math.Min(PageBottom, _balanceLimit) : PageBottom;

        private double Available => Bottom - _y;

        private double Capacity => Bottom - _regionTop;

        public IReadOnlyList<PageBox> Run()
        {
            var flow = _request.Flow;
            while (_flowIndex < flow.Count && !_converged) flow[_flowIndex].Accept(this);
            while (!_converged && _pending.Count > 0) NewPage(null);
            if (!_converged) Close();
            return _pages;
        }

        public bool VisitParagraph(ParagraphEntry original)
        {
            var entry = _request.PerPage(original, _request.PageNumber(_geometry.StyleName, _start.Number));
            var flow = entry.Flow;
            if (_line == 0 && _offset == 0)
            {
                var requested = flow.PageStyleName;
                var styleChange = requested is not null && (requested != _geometry.StyleName || flow.PageNumberRestart is { } restart && restart != _start.Number);
                if ((flow.PageBreakBefore || requested is not null) && !PageEmpty)
                {
                    NewPage(requested, restart: requested is null ? null : flow.PageNumberRestart);
                    return true;
                }
                if (styleChange && PageEmpty)
                {
                    Reopen(requested!, flow.PageNumberRestart);
                    return true;
                }
                if (requested is null && flow.NextPageStyleName is { } deferred && deferred != _geometry.StyleName)
                {
                    if (PageEmpty)
                    {
                        Reopen(deferred, flow.PageNumberRestart);
                        return true;
                    }
                    _deferred = (deferred, flow.PageNumberRestart);
                }
                if (flow.ColumnBreakBefore && !_columnEmpty)
                {
                    Advance();
                    return true;
                }
            }
            var gap = Gap(entry);
            var candidates = _line == 0 && _offset == 0 ? Candidates(entry, _y + gap) : [];
            if (Wrapped(candidates))
            {
                Dynamic(entry, gap, candidates);
                return true;
            }
            var width = ColumnWidth;
            var lines = entry.LinesFrom(width, _offset, _line);
            if (_line == 0 && _offset == 0 && flow.KeepWithNext && !_columnEmpty)
            {
                var chain = ChainHeight(_flowIndex, gap, width);
                if (chain > Available + Epsilon && chain <= Capacity + Epsilon)
                {
                    Advance();
                    return true;
                }
            }
            var remaining = Available - gap;
            var total = lines.Count;
            var fit = 0;
            var used = 0.0;
            var notes = 0.0;
            var counted = new HashSet<string>(_notes.Select(note => note.NoteId));
            while (fit < total)
            {
                var cost = NoteCost(lines[fit], counted, notes, out var added);
                if (used + lines[fit].Height + notes + cost > remaining + Epsilon) break;
                used += lines[fit].Height;
                notes += cost;
                foreach (var id in added) counted.Add(id);
                fit++;
            }
            if (fit < total && PartialNoteFits(lines[fit], counted, remaining - used - notes)) fit++;
            if (fit == total)
            {
                Place(entry, lines, total, gap, candidates);
                Finish(entry);
                return true;
            }
            var full = entry.Lines(width);
            if (_line == 0 && flow.KeepTogether && !_columnEmpty && Height(full) + flow.SpaceBefore <= Capacity + Epsilon)
            {
                Advance();
                return true;
            }
            var take = fit;
            if (_line + take < flow.Orphans && _line == 0) take = 0;
            if (total - take < flow.Widows)
            {
                take = total - flow.Widows;
                if (_line == 0 && take < flow.Orphans) take = 0;
            }
            if (take <= 0)
            {
                if (!_columnEmpty)
                {
                    Advance();
                    return true;
                }
                take = Math.Max(1, fit);
            }
            Place(entry, lines, take, gap, candidates);
            if (take >= total)
            {
                Finish(entry);
                return true;
            }
            _offset = lines[take - 1].Next;
            _line += take;
            Advance();
            return true;
        }

        public bool VisitTable(TableItem item)
        {
            var width = ColumnWidth;
            var placement = _owner.Tables.Place(item, width, _geometry.Body.Height);
            if (_row == 0 && _cells is null && item.Story == StoryKind.Body && (item.Table.PageStyleName is not null || item.Table.PageBreakBefore))
            {
                var requested = item.Table.PageStyleName;
                if (!PageEmpty)
                {
                    NewPage(requested);
                    return true;
                }
                if (requested is not null && requested != _geometry.StyleName)
                {
                    Reopen(requested, null);
                    return true;
                }
            }
            if (_row == 0 && _cells is null && !_columnEmpty)
            {
                _y += _previousAfter;
                _previousAfter = 0;
            }
            while (_row < placement.Rows.Count)
            {
                if (_columnEmpty && _row > 0 && item.HeaderRows > 0 && _row >= item.HeaderRows)
                {
                    var header = placement.Rows.Take(item.HeaderRows).Sum(row => row.Height);
                    if (header < Available)
                    {
                        Emit(placement, 0, item.HeaderRows, null, header, item);
                        _onlyHeaders = true;
                    }
                }
                if (!_columnEmpty && Available <= Epsilon)
                {
                    Advance();
                    return true;
                }
                var end = placement.GroupEnd(_row);
                var free = _columnEmpty || _onlyHeaders;
                if (_cells is null)
                {
                    var heights = Heights(placement, _row, end);
                    var height = heights.Sum();
                    var grouped = (end - _row > 1 || _carry.Count > 0) && GroupSplittable(placement, _row, end);
                    if (height + RowNotes(placement, _row, end) <= Available + Epsilon || free && !grouped && !Splittable(placement, item, _row, end))
                    {
                        var start = _row;
                        var overflow = EmitWith(placement, new TableEmission(_row, end, ColumnLeft, _y, item.Story, r => heights[r - start]), height, item);
                        if (Overflowed(placement, overflow, end)) return true;
                        _row = end;
                        continue;
                    }
                    if (grouped)
                    {
                        if (!SplitGroup(placement, item, end, free, heights)) Advance();
                        return true;
                    }
                    var outcome = Splittable(placement, item, _row, end) ? Split(placement, item, new int[placement.Rows[_row].Cells.Count], free) : SplitOutcome.None;
                    if (outcome == SplitOutcome.Finished) continue;
                    if (outcome == SplitOutcome.None) Advance();
                    return true;
                }
                var stretch = ContinuationEnd(placement);
                var continued = Continuation(placement, stretch);
                var rest = continued.Sum();
                if (rest <= Available + Epsilon)
                {
                    var first = _row;
                    var slices = placement.Rows[_row].Cells.Select((cell, c) => (c, new CellSlice(_cells[c], cell.Story.Atoms.Count, 0))).ToDictionary(pair => pair.c, pair => pair.Item2);
                    var overflow = EmitWith(placement, new TableEmission(_row, stretch, ColumnLeft, _y, item.Story, r => continued[r - first])
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
                if (again == SplitOutcome.None) Advance();
                return true;
            }
            _flowIndex++;
            _row = 0;
            _cells = null;
            _carry = [];
            _previousAfter = 0;
            _previousStyle = null;
            return true;
        }

        public bool VisitSectionStart(SectionStartItem section)
        {
            CloseRegion();
            _sections.Push(section.Columns);
            _flowIndex++;
            OpenRegion(_regionTop);
            return true;
        }

        public bool VisitSectionEnd(SectionEndItem section)
        {
            CloseRegion();
            if (_sections.Count > 0) _sections.Pop();
            _flowIndex++;
            OpenRegion(_regionTop);
            return true;
        }

        public bool VisitEndnotes(EndnotesItem endnotes)
        {
            if (!PageEmpty)
            {
                NewPage(endnotes.PageStyle, _flowIndex + 1);
                return true;
            }
            _flowIndex++;
            return true;
        }

        private double RowNotes(TablePlacement placement, int row, int end)
        {
            var ids = placement.Rows.Skip(row).Take(end - row).SelectMany(value => value.Cells).SelectMany(cell => cell.Story.Atoms).SelectMany(atom => atom.Notes);
            return NoteCost(ids, new HashSet<string>(_notes.Select(note => note.NoteId)), 0, out _);
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
            while (k < end && used + heights[k - first] + RowNotes(placement, _row, k + 1) <= Available + Epsilon)
            {
                used += heights[k - first];
                k++;
            }
            if (k == _row && end - _row == 1)
            {
                var inner = Split(placement, item, new int[placement.Rows[_row].Cells.Count], force || _carry.Count > 0);
                return inner != SplitOutcome.None;
            }
            var partial = k < end && Available - used > RowPadding(placement, k) + Epsilon && (k > _row || Starts(placement, k, Available));
            if (k == _row && !partial)
            {
                if (!force) return false;
                used = heights[0];
                k++;
                partial = k < end && Available - used > RowPadding(placement, k) + Epsilon;
            }
            var last = partial ? k + 1 : k;
            var natural = new double[last - _row];
            for (var r = _row; r < k; r++) natural[r - _row] = heights[r - first];
            if (!partial && k - _row > 0 && natural.Sum() < used) natural[k - 1 - _row] += used - natural.Sum();
            if (partial) natural[k - _row] = Available - used;
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
            var overflow = EmitWith(placement, new TableEmission(_row, last, ColumnLeft, _y, item.Story, r => natural[r - first])
            {
                Slice = (r, c) => slices.TryGetValue((r, c), out var slice) ? slice : null,
                Sliced = partial,
            }, total, item);
            _carry.AddRange(carried);
            if (!partial && Overflowed(placement, overflow, k)) return true;
            _row = k;
            _cells = next;
            Advance();
            return true;
        }

        private bool Starts(TablePlacement placement, int row, double room) =>
            placement.Rows[row].Cells.Any(cell => cell.Story.Atoms.Count > 0 && cell.Story.Atoms[0].Bottom <= room - Padding(placement, cell) + Epsilon);

        private double Padding(TablePlacement placement, CellPlacement cell)
        {
            var insets = _owner.Tables.Insets(placement.Table, cell.Cell);
            return insets.Top + insets.Bottom;
        }

        private double RowPadding(TablePlacement placement, int row) =>
            placement.Rows[row].Cells.Select(cell => Padding(placement, cell)).DefaultIfEmpty(2 * _owner.Tables.Inset(placement.Table)).Max();

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
                var available = Available - padding;
                var start = from[c];
                var origin = start == 0 ? 0 : start < atoms.Count ? atoms[start].Top : 0;
                var to = start;
                var placedNotes = new HashSet<string>(_notes.Select(note => note.NoteId));
                var noteCost = 0.0;
                while (to < atoms.Count)
                {
                    var cost = NoteCost(atoms[to].Notes, placedNotes, noteCost, out var added);
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
            var partHeight = spanning || exhausted ? Math.Max(Math.Max(used, RowPadding(placement, _row)), Available) : Math.Max(used, RowPadding(placement, _row));
            Emit(placement, _row, _row + 1, slices, partHeight, item);
            var finished = cells.Select((cell, c) => next[c] >= cell.Story.Atoms.Count).All(done => done) && !_carry.Any(carry => carry.Row < _row && carry.Row + placement.Rows[carry.Row].Cells[carry.Cell].Cell.RowSpan > _row);
            if (finished)
            {
                _cells = null;
                _row++;
                return SplitOutcome.Finished;
            }
            _cells = next;
            Advance();
            return SplitOutcome.Continued;
        }

        private void Emit(TablePlacement placement, int row, int end, IReadOnlyDictionary<int, CellSlice>? slices, double height, TableItem item) =>
            EmitWith(placement, new TableEmission(row, end, ColumnLeft, _y, item.Story, r => slices is null && end - row > 1 ? placement.Rows[r].Height : height)
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
                        if (grown == carry.From && _columnEmpty) grown++;
                        if (grown > to) extra = Math.Max(extra, atoms[grown - 1].Bottom - origin - room);
                        to = grown;
                        overflow = true;
                    }
                }
                else if (to == carry.From && emission.Sliced && _columnEmpty && to < atoms.Count)
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
            var carried = Carry(placement, emission, Math.Max(0, Available - height), out var extra, out var overflow);
            if (extra > 0)
            {
                var basis = emission.RowHeight;
                var lastRow = emission.RowEnd - 1;
                emission = emission with { RowHeight = r => basis(r) + (r == lastRow ? extra : 0) };
                height += extra;
            }
            var box = _owner.Tables.Emit(placement, emission with { Carried = carried });
            if (_tables.Count > 0 && _tables[^1] is { } last && last.TableId == box.TableId && Math.Abs(last.Bounds.X - box.Bounds.X) < Epsilon && Math.Abs(last.Bounds.Bottom - box.Bounds.Y) < Epsilon)
            {
                _tables[^1] = new TableBox(box.TableId, last.Bounds with { Height = last.Bounds.Height + box.Bounds.Height }, [.. last.Cells, .. box.Cells]);
            }
            else
            {
                _tables.Add(box);
            }
            foreach (var line in box.Lines)
            {
                _lines.Add(line);
                foreach (var note in line.Line.Notes) Reserve(note, _y + height);
            }
            _y += height;
            _columnEmpty = false;
            _onlyHeaders = false;
            return overflow;
        }

        private bool Overflowed(TablePlacement placement, bool overflow, int end)
        {
            if (!overflow) return false;
            _row = end - 1;
            _cells = [.. placement.Rows[_row].Cells.Select(cell => cell.Story.Atoms.Count)];
            Advance();
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

        private IReadOnlyList<Obstacle> Candidates(ParagraphEntry entry, double top)
        {
            var result = new List<Obstacle>();
            foreach (var id in entry.AnchoredObjects)
            {
                if (_placedObjects.Contains(id) || _request.Object(id) is not { } item) continue;
                var rect = Grown(item, _owner.Objects.Place(item, new ObjectFrame(_geometry, ColumnLeft, ColumnWidth, top)));
                var turned = RotatedRect.Around(rect, item.RotationDegrees);
                result.Add(Obstacle(item, rect, turned));
            }
            return result;
        }

        private bool Wrapped(IReadOnlyList<Obstacle> candidates)
        {
            var left = ColumnLeft;
            var band = new LayoutRect(left, _y, ColumnWidth, Math.Max(0, Bottom - _y));
            return _obstacles.Concat(candidates).Any(obstacle => Blocks(obstacle) && obstacle.Rect.Intersects(band));
        }

        private Obstacle Obstacle(AnchoredObject item, LayoutRect rect, LayoutRect turned) =>
            new(new WrapObstacle(Spaced(turned, item), item.Wrap) { Outline = _owner.Outlines.Outline(item, rect), Spread = item.WrapSpacing }, rect)
            {
                OnlyParagraph = item.WrapFirstParagraph && item.Anchor.Accept(new AnchorParagraph()) is { } paragraph ? paragraph : null,
            };

        private bool Blocks(Obstacle obstacle) => obstacle.Mode != WrapMode.Through;

        private void Dynamic(ParagraphEntry entry, double gap, IReadOnlyList<Obstacle> candidates)
        {
            var width = ColumnWidth;
            var left = ColumnLeft;
            var all = _obstacles.Concat(candidates.Where(Blocks)).Where(Blocks).Select(obstacle => obstacle.For(entry.Block.Id)).ToList();
            var firstPlaced = false;
            var startedHere = _line == 0 && _offset == 0;
            var wasEmpty = _columnEmpty;
            var rows = new List<DynamicRow>();
            if (startedHere) _y += gap;
            while (true)
            {
                var probe = entry.LineIn(width, 0, width, _offset, _line);
                var segments = _owner.Wrap.Free(all.Select(obstacle => obstacle.Shape).ToList(), left, width, _y, probe.Height);
                var below = segments.Count > 0 ? 0 : all.Where(obstacle => obstacle.Rect.Y < _y + probe.Height && obstacle.Rect.Bottom > _y).Select(obstacle => obstacle.Rect.Bottom).DefaultIfEmpty(_y + probe.Height).Min();
                if (segments.Count == 0 && below + probe.Height > Bottom + Epsilon && _columnEmpty && !firstPlaced)
                    segments = [new WrapSegment(0, width)];
                if (segments.Count == 0)
                {
                    if (below + probe.Height > Bottom + Epsilon && !_columnEmpty)
                    {
                        Interrupt(entry, rows, startedHere, wasEmpty || candidates.Count > 0);
                        return;
                    }
                    _y = Math.Max(below, _y + 1);
                    if (_y > Bottom)
                    {
                        Interrupt(entry, rows, startedHere, wasEmpty || candidates.Count > 0);
                        return;
                    }
                    continue;
                }
                var pieces = new List<(ComposedLine Line, double Left)>();
                var start = _offset;
                var index = _line;
                for (var s = 0; s < segments.Count; s++)
                {
                    var segment = segments[s];
                    var line = entry.LineIn(width, segment.Left, segment.Right, start, index);
                    if (line.Broken && segments.Count > 1 && (s < segments.Count - 1 || pieces.Count > 0)) continue;
                    index++;
                    pieces.Add((line, segment.Left));
                    start = line.Next;
                    if (line.IsLast || line.Forced || start >= entry.Shaped.Text.Length) break;
                }
                var height = pieces.Max(piece => piece.Line.Height);
                if (_y + height > Bottom + Epsilon && (!_columnEmpty || firstPlaced))
                {
                    Interrupt(entry, rows, startedHere, wasEmpty || candidates.Count > 0);
                    return;
                }
                rows.Add(new DynamicRow(pieces.Count, _y, _line, _offset));
                foreach (var (line, offset) in pieces)
                {
                    var box = new LineBox(line, left + offset, _y) { Story = entry.Story, StoryId = entry.StoryId };
                    AddLine(entry, box, left, width);
                }
                if (!firstPlaced && _line == 0) Commit(candidates);
                firstPlaced = true;
                _y += height;
                _line = index;
                _offset = start;
                _columnEmpty = false;
                if (pieces[^1].Line.IsLast)
                {
                    Finish(entry);
                    return;
                }
            }
        }

        private void Interrupt(ParagraphEntry entry, List<DynamicRow> rows, bool startedHere, bool anchored)
        {
            var keep = rows.Count;
            if (keep > 0)
            {
                var flow = entry.Flow;
                var remaining = entry.LinesFrom(ColumnWidth, _offset, _line).Count;
                if (remaining < flow.Widows)
                {
                    var moved = 0;
                    while (keep > 0 && moved < flow.Widows - remaining) moved += rows[--keep].Lines;
                }
                if (startedHere && rows.Take(keep).Sum(row => row.Lines) < flow.Orphans) keep = 0;
                if (keep == 0 && anchored) keep = rows.Count;
                if (keep < rows.Count)
                {
                    var removed = rows.Skip(keep).Sum(row => row.Lines);
                    var dropped = new HashSet<LineBox>(_lines.Skip(_lines.Count - removed));
                    _lines.RemoveRange(_lines.Count - removed, removed);
                    _decorated.RemoveAll(item => dropped.Contains(item.Line));
                    var back = rows[keep];
                    _y = back.Y;
                    _line = back.Line;
                    _offset = back.Offset;
                }
            }
            Advance();
        }

        private void Commit(IReadOnlyList<Obstacle> candidates)
        {
            if (candidates.Count == 0) return;
            var entry = (ParagraphEntry)_request.Flow[_flowIndex];
            var index = 0;
            foreach (var id in entry.AnchoredObjects)
            {
                if (_placedObjects.Contains(id) || _request.Object(id) is not { } item) continue;
                var obstacle = candidates[index++];
                AddObject(item, obstacle.Area);
            }
        }

        private void AddObject(AnchoredObject item, LayoutRect rect)
        {
            _placedObjects.Add(item.Id);
            var layer = item.InBackground ? ObjectLayer.Behind : item.Wrap == WrapMode.Through ? ObjectLayer.Front : ObjectLayer.Wrapped;
            rect = Grown(item, rect);
            var content = FrameContent(item.Id, rect);
            _objects.Add(new ObjectBox(item.Id, rect, layer) { Content = content, RotationDegrees = item.RotationDegrees, FlipHorizontal = item.FlipHorizontal, FlipVertical = item.FlipVertical });
            foreach (var line in content.OfType<LineBox>().Concat(content.OfType<TableBox>().SelectMany(table => table.Lines))) InlineObjects(line);
            var turned = RotatedRect.Around(rect, item.RotationDegrees);
            if (layer == ObjectLayer.Wrapped) _obstacles.Add(Obstacle(item, rect, turned));
        }

        private LayoutRect Grown(AnchoredObject item, LayoutRect rect)
        {
            if (_request.Frame(item.Id) is not { } frame) return rect;
            var height = frame.Height + 2 * _request.FrameInset(item.Id);
            return rect.Height < height ? rect with { Height = height } : rect;
        }

        private LayoutRect Spaced(LayoutRect rect, AnchoredObject item)
        {
            var spacing = item.WrapSpacing;
            return new LayoutRect(
                rect.X - spacing.Left.ToPoints(),
                rect.Y - spacing.Top.ToPoints(),
                rect.Width + spacing.Left.ToPoints() + spacing.Right.ToPoints(),
                rect.Height + spacing.Top.ToPoints() + spacing.Bottom.ToPoints());
        }

        private double NoteCost(ComposedLine line, HashSet<string> counted, double already, out List<string> added) => NoteCost(line.Notes, counted, already, out added);

        private double NoteCost(IEnumerable<string> notes, HashSet<string> counted, double already, out List<string> added)
        {
            added = [];
            var cost = 0.0;
            var area = NoteArea;
            foreach (var id in notes)
            {
                if (counted.Contains(id) || added.Contains(id)) continue;
                var story = _request.Note(id, NoteWidth);
                if (story is null) continue;
                if (!_notes.Any(note => note.Area == area) && already == 0 && cost == 0) cost += Separator;
                cost += story.Height;
                added.Add(id);
            }
            return cost;
        }

        private bool PartialNoteFits(ComposedLine line, HashSet<string> counted, double remaining)
        {
            var id = line.Notes.FirstOrDefault(note => !counted.Contains(note));
            if (id is null || _request.Note(id, NoteWidth) is not { Atoms.Count: > 0 } story) return false;
            var area = NoteArea;
            var separator = _notes.Any(note => note.Area == area) || counted.Except(_notes.Select(note => note.NoteId)).Any() ? 0 : Separator;
            return line.Height + separator + story.Atoms[0].Bottom <= remaining + Epsilon;
        }

        private double Separator => _owner.Options.SeparatorBefore + _owner.Options.SeparatorWidth + _owner.Options.SeparatorAfter;

        private void Reserve(string noteId, double contentBottom)
        {
            if (_notes.Any(note => note.NoteId == noteId) || _pending.Any(note => note.NoteId == noteId)) return;
            var story = _request.Note(noteId, NoteWidth);
            if (story is null || story.Atoms.Count == 0) return;
            if (PerColumn) Take(noteId, story, 0, contentBottom, _column, ColumnLeft, ColumnWidth);
            else Take(noteId, story, 0, Math.Max(contentBottom, OtherBottoms()), -1, _geometry.Body.X, _geometry.Body.Width);
        }

        private void Take(string noteId, ComposedStory story, int from, double contentBottom, int area, double left, double width)
        {
            var separator = _notes.Any(note => note.Area == area) ? 0 : Separator;
            var used = area >= 0 ? _columnNotes[area] : _noteHeight;
            var limit = _geometry.Body.Height - _geometry.Content.Height * _owner.Options.BodyReserve - used - separator;
            var space = Math.Min(_geometry.Body.Bottom - _noteHeight - (area >= 0 ? _columnNotes[area] : 0) - separator - contentBottom, limit);
            var to = from;
            while (to < story.Atoms.Count && Slice(story, from, to + 1) <= space + Epsilon) to++;
            to = Kept(story, from, to);
            if (to == from && contentBottom <= _geometry.Body.Y + Epsilon) to = from + 1;
            if (to == from)
            {
                _pending.Add(new PendingNote(noteId, from));
                return;
            }
            _notes.Add(new PlacedNote(noteId, story, from, to, area, left, width));
            if (area >= 0) _columnNotes[area] += separator + Slice(story, from, to);
            else _noteHeight += separator + Slice(story, from, to);
            if (to < story.Atoms.Count) _pending.Add(new PendingNote(noteId, to));
        }

        private int Kept(ComposedStory story, int from, int to)
        {
            var atoms = story.Atoms;
            if (to <= from || to >= atoms.Count || atoms[to - 1].Paragraph is not { } entry || !ReferenceEquals(atoms[to].Paragraph, entry)) return to;
            var first = to - 1;
            while (first > 0 && ReferenceEquals(atoms[first - 1].Paragraph, entry)) first--;
            var last = to;
            while (last + 1 < atoms.Count && ReferenceEquals(atoms[last + 1].Paragraph, entry)) last++;
            var flow = entry.Flow;
            var head = to - Math.Max(first, from);
            var tail = last + 1 - to;
            var cut = to;
            if (tail < flow.Widows) cut = last + 1 - flow.Widows;
            if (first >= from && cut - first < flow.Orphans) cut = first;
            if (first < from && cut <= from) cut = to;
            return Math.Max(from, Math.Min(cut, to));
        }

        private double Slice(ComposedStory story, int from, int to)
        {
            var bottom = to >= story.Atoms.Count ? story.Height : story.Atoms[to - 1].Bottom;
            var top = from == 0 ? 0 : story.Atoms[from].Top;
            return bottom - top;
        }

        private double OtherBottoms()
        {
            var bottom = _y;
            for (var c = 0; c < _columnBottoms.Length; c++) bottom = Math.Max(bottom, _columnBottoms[c]);
            return bottom;
        }

        private double Gap(ParagraphEntry entry)
        {
            if (_line > 0 || _offset > 0) return 0;
            var flow = entry.Flow;
            if (_columnEmpty) return SpaceAtTop(flow) ? flow.SpaceBefore : 0;
            if (flow.Contextual && flow.StyleName == _previousStyle) return 0;
            return _previousAfter + flow.SpaceBefore;
        }

        private bool SpaceAtTop(ParagraphFlow flow) =>
            _column == 0 && (_pages.Count == 0 || flow.PageBreakBefore || flow.PageStyleName is not null);

        private double ChainHeight(int from, double gap, double width)
        {
            var flow = _request.Flow;
            var height = gap;
            for (var index = from; index < flow.Count; index++)
            {
                if (flow[index] is not ParagraphEntry entry) return height;
                var lines = entry.Lines(width);
                if (index > from && flow[index - 1] is ParagraphEntry previous) height += previous.Flow.SpaceAfter + entry.Flow.SpaceBefore;
                var previousKeeps = index > from && flow[index - 1] is ParagraphEntry before && before.Flow.KeepWithNext;
                if (!entry.Flow.KeepWithNext || index > from && !previousKeeps)
                {
                    var head = index == from ? lines.Count : MinimalHead(lines.Count, entry.Flow);
                    return height + Height(lines.Take(head));
                }
                height += Height(lines);
                if (height > Capacity) return height;
            }
            return height;
        }

        private int MinimalHead(int count, ParagraphFlow flow)
        {
            var orphans = Math.Max(1, flow.Orphans);
            if (flow.KeepTogether || count < orphans + Math.Max(1, flow.Widows)) return count;
            return orphans;
        }

        private double Height(IEnumerable<ComposedLine> lines) => lines.Sum(line => line.Height);

        private void Place(ParagraphEntry entry, IReadOnlyList<ComposedLine> lines, int count, double gap, IReadOnlyList<Obstacle> candidates)
        {
            _y += gap;
            var left = ColumnLeft;
            var width = ColumnWidth;
            if (_line == 0 && _offset == 0) Commit(candidates);
            for (var i = 0; i < count && i < lines.Count; i++)
            {
                var line = lines[i];
                var box = new LineBox(line, left, _y) { Story = entry.Story, StoryId = entry.StoryId };
                _y += line.Height;
                AddLine(entry, box, left, width);
                foreach (var note in line.Notes) Reserve(note, _y);
            }
            _columnEmpty = false;
        }

        private void AddLine(ParagraphEntry entry, LineBox box, double left, double width)
        {
            _lines.Add(box);
            if (entry.Decoration is not null) _decorated.Add((entry, box, left, width));
            InlineObjects(box);
        }

        private IReadOnlyList<LayoutBox> FrameContent(string id, LayoutRect rect)
        {
            if (_request.Frame(id) is { } frame) return StoryIn(frame, rect, _request.FrameInset(id), _request.FrameAlignment(id), id);
            return [.. _request.MemberFrames(id).SelectMany(member => StoryIn(member.Story,
                new LayoutRect(rect.X + member.X * rect.Width, rect.Y + member.Y * rect.Height, member.Width * rect.Width, member.Height * rect.Height),
                member.Inset, member.Alignment, id))];
        }

        private IReadOnlyList<LayoutBox> StoryIn(ComposedStory frame, LayoutRect rect, double inset, double alignment, string id)
        {
            var slack = Math.Max(0, rect.Height - 2 * inset - frame.Height) * alignment;
            return _owner.Stories.Emit(frame, 0, frame.Atoms.Count, rect.X + inset, rect.Y + inset + slack + (frame.Atoms.Count > 0 ? frame.Atoms[0].Top : 0), Math.Max(1, rect.Width - 2 * inset), StoryKind.Frame, id);
        }

        private void InlineObjects(LineBox box)
        {
            foreach (var run in box.Line.Runs.Where(run => run.Kind == GlyphRunKind.Object && run.ObjectId is not null).ToList())
            {
                var height = run.Ascent;
                var item = _request.Object(run.ObjectId!);
                var rect = new LayoutRect(box.Left + run.X, box.Baseline - height, run.Width, height);
                var content = item is null ? [] : FrameContent(item.Id, rect);
                _objects.Add(new ObjectBox(run.ObjectId!, rect, ObjectLayer.Inline)
                {
                    Content = content,
                    RotationDegrees = item?.RotationDegrees ?? 0,
                    FlipHorizontal = item?.FlipHorizontal ?? false,
                    FlipVertical = item?.FlipVertical ?? false,
                });
            }
        }

        private void Finish(ParagraphEntry entry)
        {
            _previousAfter = entry.Flow.SpaceAfter;
            _previousStyle = entry.Flow.StyleName;
            _flowIndex++;
            _line = 0;
            _offset = 0;
        }

        private void Advance()
        {
            _columnBottoms[_column] = Math.Max(_columnBottoms[_column], _y);
            if (_column < _region.Count - 1)
            {
                _column++;
                _y = _regionTop;
                _columnEmpty = true;
                _onlyHeaders = false;
                ContinueNotes();
                return;
            }
            NewPage(null);
        }

        private void ContinueNotes()
        {
            if (!PerColumn || _pending.Count == 0) return;
            var pending = _pending.ToList();
            _pending.Clear();
            foreach (var note in pending)
            {
                var story = _request.Note(note.NoteId, ColumnWidth);
                if (story is not null && note.Atom < story.Atoms.Count) Take(note.NoteId, story, note.Atom, _y, _column, ColumnLeft, ColumnWidth);
            }
        }

        private void OpenRegion(double top)
        {
            _region = _sections.Count > 0 ? _sections.Peek() : _geometry.Columns;
            if (_columnNotes.Length < _region.Count) Array.Resize(ref _columnNotes, _region.Count);
            _regionTop = top;
            _y = top;
            _column = 0;
            _columnBottoms = new double[_region.Count];
            for (var c = 0; c < _columnBottoms.Length; c++) _columnBottoms[c] = top;
            _columnEmpty = PageEmpty;
            _balanceLimit = double.PositiveInfinity;
            if (_region.Count > 1 && _region.Balanced)
            {
                var height = _owner.Balancer.Balance(new BalanceRequest(_request.Flow, _flowIndex, _line, _offset, _row, _region, _region.Width(_geometry.Body.Width), PageBottom - top) { TurnedLimit = _geometry.Body.Height });
                _balanceLimit = top + height;
            }
        }

        private void CloseRegion()
        {
            if (_columnBottoms.Length == 0) return;
            _columnBottoms[_column] = Math.Max(_columnBottoms[_column], _y);
            var bottom = _columnBottoms.Max();
            if (_region.Count > 1)
            {
                for (var c = 0; c < _region.Count; c++)
                {
                    var x = _region.Left(_geometry.Body.X, _geometry.Body.Width, c);
                    _columnAreas.Add(new AreaBox(AreaKind.Column, new LayoutRect(x, _regionTop, _region.Width(_geometry.Body.Width), bottom - _regionTop), []));
                    if (_region.Separator && c > 0)
                    {
                        var line = x - _region.Spacing(_geometry.Body.Width) / 2;
                        _decorations.Add(new DecorationBox(DecorationKind.ColumnSeparator, new LayoutRect(line, _regionTop, 0, bottom - _regionTop), null, 0.5));
                    }
                }
            }
            _regionTop = bottom;
            _y = bottom;
            _columnBottoms = [];
        }

        private void Open(string style, IReadOnlyList<PendingNote> pending)
        {
            var number = _pages.Count + 1;
            var first = _pages.Count == 0 || _pages[^1].StyleName != style;
            var basis = _request.Geometry(style, new PageSide(_start.Number, first) { Count = _request.PageCount });
            _geometry = basis;
            _noteHeight = 0;
            _columnNotes = new double[Math.Max(1, basis.Columns.Count)];
            _y = basis.Body.Y;
            _previousAfter = 0;
            _previousStyle = null;
            _columnEmpty = true;
            _onlyHeaders = false;
            _obstacles.Clear();
            foreach (var item in _request.PageObjects(number))
            {
                AddObject(item, _owner.Objects.Place(item, new ObjectFrame(_geometry, _geometry.Body.X, _geometry.Body.Width, _geometry.Body.Y)));
            }
            var columns = _sections.Count == 0 && basis.Columns.Count > 1;
            var noteWidth = columns ? basis.Columns.Width(basis.Body.Width) : basis.Body.Width;
            var noteLeft = columns ? basis.Columns.Left(basis.Body.X, basis.Body.Width, 0) : basis.Body.X;
            foreach (var note in pending)
            {
                var story = _request.Note(note.NoteId, noteWidth);
                if (story is not null && note.Atom < story.Atoms.Count) Take(note.NoteId, story, note.Atom, _geometry.Body.Y, columns ? 0 : -1, noteLeft, noteWidth);
            }
            OpenRegion(_geometry.Body.Y);
        }

        private void Reopen(string style, int? restart)
        {
            _start = _start with { Style = style, Number = restart ?? _start.Number };
            _lines.Clear();
            _columnAreas.Clear();
            _objects.Clear();
            _decorations.Clear();
            _placedObjects.Clear();
            var notes = _notes.Select(note => new PendingNote(note.NoteId, note.From)).Concat(_pending).ToList();
            _notes.Clear();
            _pending.Clear();
            Open(style, notes);
        }

        private void Close()
        {
            CloseRegion();
            foreach (var group in _decorated.GroupBy(item => (item.Entry, item.Left)))
            {
                _decorations.InsertRange(0, _owner.Decorator.Decorate(group.Key.Entry, [.. group.Select(item => item.Line)], group.Key.Left, group.First().Width));
            }
            AreaBox? footnotes = null;
            if (_notes.Count > 0)
            {
                var content = new List<LayoutBox>();
                var bounds = new List<LayoutRect>();
                foreach (var group in _notes.GroupBy(note => note.Area))
                {
                    var height = group.Key >= 0 ? _columnNotes[group.Key] : _noteHeight;
                    var top = _geometry.Body.Bottom - _noteHeight - (group.Key >= 0 ? height : 0);
                    var left = group.First().Left;
                    var width = group.First().Width;
                    content.Add(new DecorationBox(DecorationKind.FootnoteSeparator, new LayoutRect(left, top + _owner.Options.SeparatorBefore, width * _owner.Options.SeparatorRelativeWidth, _owner.Options.SeparatorWidth), null, _owner.Options.SeparatorWidth));
                    var y = top + Separator;
                    foreach (var note in group)
                    {
                        var origin = note.From == 0 ? 0 : note.Story.Atoms[note.From].Top;
                        var first = note.Story.Atoms[note.From].Top - origin;
                        content.AddRange(_owner.Stories.Emit(note.Story, note.From, note.To, left, y + first, width, StoryKind.Footnote, note.NoteId));
                        y += Slice(note.Story, note.From, note.To);
                    }
                    bounds.Add(new LayoutRect(left, top, width, height));
                }
                var x = bounds.Min(rect => rect.X);
                var areaTop = bounds.Min(rect => rect.Y);
                footnotes = new AreaBox(AreaKind.Footnotes, new LayoutRect(x, areaTop, bounds.Max(rect => rect.Right) - x, bounds.Max(rect => rect.Bottom) - areaTop), content);
            }
            var body = new PageContent
            {
                Lines = [.. _lines],
                Tables = [.. _tables],
                Columns = [.. _columnAreas],
                Footnotes = footnotes,
                Objects = [.. _objects],
                Decorations = [.. _decorations],
                Body = _geometry.Body,
            };
            _pages.Add(new PageBox(_pages.Count, _geometry.StyleName, _geometry.Width, _geometry.Height, _geometry.Content, body, _start, PageStories.None)
            {
                Backdrop = _owner.Backdrops.Build(_geometry),
            });
            _lines.Clear();
            _tables.Clear();
            _columnAreas.Clear();
            _objects.Clear();
            _decorations.Clear();
            _decorated.Clear();
            _notes.Clear();
            _placedObjects.Clear();
        }

        private void NewPage(string? style, int? flowIndex = null, int? restart = null)
        {
            var deferred = style is null ? _deferred : null;
            _deferred = null;
            var number = restart ?? deferred?.Restart ?? _start.Number + 1;
            var next = style ?? deferred?.Style ?? _geometry.NextStyleName;
            Close();
            if (flowIndex is { } index) _flowIndex = index;
            var pending = _pending.ToList();
            _pending.Clear();
            _start = new PageStart(_flowIndex, _line, next)
            {
                Number = number,
                Offset = _offset,
                Row = _row,
                Cells = _cells is null ? ValueList<int>.Empty : new ValueList<int>([.. _cells]),
                Pending = new ValueList<PendingNote>(pending),
                Carried = new ValueList<CarriedCell>([.. _carry]),
            };
            var remainder = _request.Probe?.Remainder(_pages.Count, _start);
            if (remainder is not null)
            {
                _pages.AddRange(remainder);
                _converged = true;
                return;
            }
            Open(next, pending);
        }
    }
}
