using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Objects;

namespace PulseOffice.Documents.Layout.Flow;

internal sealed record DynamicRow(int Lines, double Y, int Line, int Offset);

internal sealed class PaginationRun : IFlowItemVisitor<bool>
{
    private const double Epsilon = 0.001;

    private readonly Paginator _owner;
    private readonly PaginationRequest _request;
    private readonly List<PageBox> _pages;
    private readonly List<LineBox> _lines = [];
    private readonly List<TableBox> _tables = [];
    private readonly List<AreaBox> _columnAreas = [];
    private readonly List<DecorationBox> _decorations = [];
    private readonly FootnoteFlow _footnotes;
    private readonly TableFlow _tableFlow;
    private readonly ObjectFlow _anchored;
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
    private double _previousAfter;
    private string? _previousStyle;
    private bool _columnEmpty = true;
    private bool _converged;
    private int _flowIndex;
    private int _line;
    private int _offset;

    public PaginationRun(Paginator owner, PaginationRequest request)
    {
        _owner = owner;
        _request = request;
        _footnotes = new FootnoteFlow(this, request, owner.Stories, owner.Options);
        _anchored = new ObjectFlow(this, request, owner.Objects, owner.Stories, owner.Outlines);
        _pages = [.. request.Prefix];
        var resume = request.Resume;
        _tableFlow = new TableFlow(this, owner.Tables, resume);
        _flowIndex = resume.FlowIndex;
        _line = resume.Line;
        _offset = resume.Offset;
        for (var i = 0; i < Math.Min(_flowIndex, request.Flow.Count); i++)
        {
            if (request.Flow[i] is SectionStartItem start) _sections.Push(start.Columns);
            else if (request.Flow[i] is SectionEndItem && _sections.Count > 0) _sections.Pop();
        }
        _start = resume;
        Open(resume.Style, resume.Pending.Items);
    }

    internal FootnoteFlow Footnotes => _footnotes;

    internal List<LineBox> Lines => _lines;

    internal List<TableBox> Tables => _tables;

    internal bool ColumnEmpty
    {
        get => _columnEmpty;
        set => _columnEmpty = value;
    }

    internal bool PageEmpty => _lines.Count == 0 && _tables.Count == 0;

    internal PageGeometry Geometry => _geometry;

    internal bool EvenPage { get; private set; }

    internal double Y
    {
        get => _y;
        set => _y = value;
    }

    internal double PreviousAfter
    {
        get => _previousAfter;
        set => _previousAfter = value;
    }

    internal int Column => _column;

    internal double ColumnWidth => _region.Width(_geometry.Body.Width, _column);

    internal double ColumnLeft => _region.Left(_geometry.Body.X, _geometry.Body.Width, _column);

    private double PageBottom => _geometry.Body.Bottom - _footnotes.PageHeight - (PerColumn ? _footnotes.ColumnHeight(_column) : 0);

    internal bool PerColumn => _region.Count > 1 && (_sections.Count == 0 || !_region.Balanced);

    internal int NoteArea => PerColumn ? _column : -1;

    internal double NoteWidth => PerColumn ? ColumnWidth : _geometry.Body.Width;

    internal double Bottom => _column < _region.Count - 1 ? Math.Min(PageBottom, _balanceLimit) : PageBottom;

    internal double Available => Bottom - _y;

    private double Capacity => Bottom - _regionTop;

    public IReadOnlyList<PageBox> Run()
    {
        var flow = _request.Flow;
        while (_flowIndex < flow.Count && !_converged) flow[_flowIndex].Accept(this);
        while (!_converged && _footnotes.HasPending) NewPage(null);
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
        var candidates = _line == 0 && _offset == 0 ? _anchored.Candidates(entry, _y + gap) : [];
        if (_anchored.Wrapped(candidates))
        {
            if (_line == 0 && _offset == 0 && _anchored.HeightDependent(entry)) candidates = Settled(entry, _y + gap, candidates);
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
        var counted = _footnotes.PlacedIds();
        while (fit < total)
        {
            var cost = _footnotes.Cost(lines[fit], counted, notes, out var added);
            if (used + lines[fit].Height + notes + cost > remaining + Epsilon) break;
            used += lines[fit].Height;
            notes += cost;
            foreach (var id in added) counted.Add(id);
            fit++;
        }
        if (fit < total && _footnotes.PartialFits(lines[fit], counted, remaining - used - notes)) fit++;
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

    public bool VisitTable(TableItem item) => _tableFlow.Place(item);

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

    private const int SettlePasses = 4;
    private const double SettleTolerance = 0.5;

    private IReadOnlyList<Obstacle> Settled(ParagraphEntry entry, double top, IReadOnlyList<Obstacle> candidates)
    {
        for (var pass = 0; pass < SettlePasses; pass++)
        {
            var height = WrappedHeight(entry, top, candidates);
            var next = _anchored.Candidates(entry, top, height);
            var moved = next.Count != candidates.Count || next.Where((obstacle, i) => Math.Abs(obstacle.Area.Y - candidates[i].Area.Y) > SettleTolerance).Any();
            candidates = next;
            if (!moved) break;
        }
        return candidates;
    }

    private double WrappedHeight(ParagraphEntry entry, double top, IReadOnlyList<Obstacle> candidates)
    {
        var width = ColumnWidth;
        var left = ColumnLeft;
        var all = _anchored.Blocking(candidates, entry.Block.Id).Select(obstacle => obstacle.Shape).ToList();
        var y = top;
        var offset = 0;
        var index = 0;
        while (y <= Bottom)
        {
            var probe = entry.LineIn(width, 0, width, offset, index);
            var segments = _owner.Wrap.Free(all, left, width, y, probe.Height);
            if (segments.Count == 0)
            {
                var below = all.Where(shape => shape.Rect.Y < y + probe.Height && shape.Rect.Bottom > y).Select(shape => shape.Rect.Bottom).DefaultIfEmpty(y + probe.Height).Min();
                y = Math.Max(below, y + 1);
                continue;
            }
            var height = 0.0;
            var last = false;
            var placed = 0;
            for (var s = 0; s < segments.Count; s++)
            {
                var line = entry.LineIn(width, segments[s].Left, segments[s].Right, offset, index);
                if (line.Broken && segments.Count > 1 && (s < segments.Count - 1 || placed > 0)) continue;
                placed++;
                index++;
                offset = line.Next;
                height = Math.Max(height, line.Height);
                last = line.IsLast;
                if (line.IsLast || line.Forced || offset >= entry.Shaped.Text.Length) break;
            }
            if (placed == 0)
            {
                y += probe.Height;
                continue;
            }
            y += height;
            if (last) break;
        }
        return y - top;
    }

    private void Dynamic(ParagraphEntry entry, double gap, IReadOnlyList<Obstacle> candidates)
    {
        var width = ColumnWidth;
        var left = ColumnLeft;
        var all = _anchored.Blocking(candidates, entry.Block.Id).ToList();
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
            if (!firstPlaced && _line == 0) _anchored.Commit((ParagraphEntry)_request.Flow[_flowIndex], candidates);
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

    internal double OtherBottoms()
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
            if (index > from && flow[index] is TableItem table && flow[index - 1] is ParagraphEntry keeper) return height + keeper.Flow.SpaceAfter + FirstRows(table, width);
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

    private double FirstRows(TableItem table, double width)
    {
        var placement = _owner.Tables.Place(table, width, _geometry.Body.Height);
        if (placement.Rows.Count == 0) return 0;
        var end = placement.GroupEnd(0);
        while (end < placement.Rows.Count && end <= table.HeaderRows) end = placement.GroupEnd(end);
        var height = 0.0;
        for (var row = 0; row < end; row++) height += placement.Rows[row].Height;
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
        if (_line == 0 && _offset == 0) _anchored.Commit((ParagraphEntry)_request.Flow[_flowIndex], candidates);
        for (var i = 0; i < count && i < lines.Count; i++)
        {
            var line = lines[i];
            var box = new LineBox(line, left, _y) { Story = entry.Story, StoryId = entry.StoryId };
            _y += line.Height;
            AddLine(entry, box, left, width);
            foreach (var note in line.Notes) _footnotes.Reserve(note, _y);
        }
        _columnEmpty = false;
    }

    private void AddLine(ParagraphEntry entry, LineBox box, double left, double width)
    {
        _lines.Add(box);
        if (entry.Decoration is not null) _decorated.Add((entry, box, left, width));
        _anchored.Inline(box);
    }

    internal void FinishTable()
    {
        _flowIndex++;
        _previousAfter = 0;
        _previousStyle = null;
    }

    private void Finish(ParagraphEntry entry)
    {
        _previousAfter = entry.Flow.SpaceAfter;
        _previousStyle = entry.Flow.StyleName;
        _flowIndex++;
        _line = 0;
        _offset = 0;
    }

    internal void Advance()
    {
        _columnBottoms[_column] = Math.Max(_columnBottoms[_column], _y);
        if (_column < _region.Count - 1)
        {
            _column++;
            _y = _regionTop;
            _columnEmpty = true;
            _tableFlow.NewColumn();
            _footnotes.ContinueInColumn();
            return;
        }
        NewPage(null);
    }

    private void OpenRegion(double top)
    {
        _region = _sections.Count > 0 ? _sections.Peek() : _geometry.Columns;
        _footnotes.Ensure(_region.Count);
        _regionTop = top;
        _y = top;
        _column = 0;
        _columnBottoms = new double[_region.Count];
        for (var c = 0; c < _columnBottoms.Length; c++) _columnBottoms[c] = top;
        _columnEmpty = PageEmpty;
        _balanceLimit = double.PositiveInfinity;
        if (_region.Count > 1 && _region.Balanced)
        {
            var height = _owner.Balancer.Balance(new BalanceRequest(_request.Flow, _flowIndex, _line, _offset, _tableFlow.Row, _region, _region.Width(_geometry.Body.Width), PageBottom - top) { TurnedLimit = _geometry.Body.Height });
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
                _columnAreas.Add(new AreaBox(AreaKind.Column, new LayoutRect(x, _regionTop, _region.Width(_geometry.Body.Width, c), bottom - _regionTop), []));
                if (_region.Separator && c > 0)
                {
                    var line = x - _region.GapBefore(_geometry.Body.Width, c) / 2;
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
        var basis = _request.Geometry(style, new PageSide(_start.Number, first) { Count = _request.PageCount, Physical = number });
        _geometry = basis;
        _footnotes.Reset(basis.Columns.Count);
        _y = basis.Body.Y;
        _previousAfter = 0;
        _previousStyle = null;
        _columnEmpty = true;
        _tableFlow.NewColumn();
        EvenPage = number % 2 == 0;
        _anchored.OpenPage(number);
        var columns = _sections.Count == 0 && basis.Columns.Count > 1;
        var noteWidth = columns ? basis.Columns.Width(basis.Body.Width) : basis.Body.Width;
        var noteLeft = columns ? basis.Columns.Left(basis.Body.X, basis.Body.Width, 0) : basis.Body.X;
        _footnotes.Resume(pending, columns ? 0 : -1, noteLeft, noteWidth);
        OpenRegion(_geometry.Body.Y);
    }

    internal void Reopen(string style, int? restart)
    {
        _start = _start with { Style = style, Number = restart ?? _start.Number };
        _lines.Clear();
        _columnAreas.Clear();
        _anchored.Clear();
        _decorations.Clear();
        var notes = _footnotes.Withdraw();
        Open(style, notes);
    }

    private void Close()
    {
        CloseRegion();
        foreach (var group in _decorated.GroupBy(item => (item.Entry, item.Left)))
        {
            _decorations.InsertRange(0, _owner.Decorator.Decorate(group.Key.Entry, [.. group.Select(item => item.Line)], group.Key.Left, group.First().Width));
        }
        var footnotes = _footnotes.Area(_y);
        var body = new PageContent
        {
            Lines = [.. _lines],
            Tables = [.. _tables],
            Columns = [.. _columnAreas],
            Footnotes = footnotes,
            Objects = [.. _anchored.Boxes],
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
        _anchored.Clear();
        _decorations.Clear();
        _decorated.Clear();
        _footnotes.ClearPlaced();
    }

    internal void NewPage(string? style, int? flowIndex = null, int? restart = null)
    {
        var deferred = style is null ? _deferred : null;
        _deferred = null;
        var number = restart ?? deferred?.Restart ?? _start.Number + 1;
        var next = style ?? deferred?.Style ?? _geometry.NextStyleName;
        Close();
        if (flowIndex is { } index) _flowIndex = index;
        var pending = _footnotes.TakePending();
        _start = _tableFlow.Mark(new PageStart(_flowIndex, _line, next)
        {
            Number = number,
            Offset = _offset,
            Pending = new ValueList<PendingNote>(pending),
        });
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
