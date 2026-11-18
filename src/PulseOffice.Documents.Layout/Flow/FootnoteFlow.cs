using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.Layout.Flow;

internal sealed record PlacedNote(string NoteId, ComposedStory Story, int From, int To, int Area, double Left, double Width)
{
    public ComposedStory? Backward { get; init; }

    public ComposedStory? Forward { get; init; }

    public double Height(double slice) => (Backward?.Height ?? 0) + slice + (Forward?.Height ?? 0);
}

internal sealed class FootnoteFlow(PaginationRun run, PaginationRequest request, IStoryComposer stories, PageFlowOptions options)
{
    private const double Epsilon = 0.001;

    private readonly List<PlacedNote> _notes = [];
    private readonly List<PendingNote> _pending = [];
    private double _noteHeight;
    private double[] _columnNotes = [0];

    public double PageHeight => _noteHeight;

    public bool HasPending => _pending.Count > 0;

    private PulseOffice.Documents.Annotations.NoteSeparator Rule => request.PageSeparator?.Invoke(run.Geometry.StyleName) ?? request.Separator;

    private double Separator => Rule.SpaceBefore + Rule.Thickness + Rule.SpaceAfter;

    public double ColumnHeight(int column) => _columnNotes[column];

    public HashSet<string> PlacedIds() => new(_notes.Select(note => note.NoteId));

    public double Cost(ComposedLine line, HashSet<string> counted, double already, out List<string> added) => Cost(line.Notes, counted, already, out added);

    public double Cost(IEnumerable<string> notes, HashSet<string> counted, double already, out List<string> added)
    {
        added = [];
        var cost = 0.0;
        var area = run.NoteArea;
        foreach (var id in notes)
        {
            if (counted.Contains(id) || added.Contains(id)) continue;
            var story = request.Note(id, run.NoteWidth);
            if (story is null) continue;
            if (!_notes.Any(note => note.Area == area) && already == 0 && cost == 0) cost += Separator;
            cost += story.Height;
            added.Add(id);
        }
        return cost;
    }

    public bool PartialFits(ComposedLine line, HashSet<string> counted, double remaining)
    {
        var id = line.Notes.FirstOrDefault(note => !counted.Contains(note));
        if (id is null || request.Note(id, run.NoteWidth) is not { Atoms.Count: > 0 } story) return false;
        var area = run.NoteArea;
        var separator = _notes.Any(note => note.Area == area) || counted.Except(_notes.Select(note => note.NoteId)).Any() ? 0 : Separator;
        return line.Height + separator + story.Atoms[0].Bottom <= remaining + Epsilon;
    }

    public void Reserve(string noteId, double contentBottom)
    {
        if (_notes.Any(note => note.NoteId == noteId) || _pending.Any(note => note.NoteId == noteId)) return;
        var story = request.Note(noteId, run.NoteWidth);
        if (story is null || story.Atoms.Count == 0) return;
        var geometry = run.Geometry;
        if (run.PerColumn) Take(noteId, story, 0, contentBottom, run.Column, run.ColumnLeft, run.ColumnWidth);
        else Take(noteId, story, 0, Math.Max(contentBottom, run.OtherBottoms()), -1, geometry.Body.X, geometry.Body.Width);
    }

    public void ContinueInColumn()
    {
        if (!run.PerColumn || _pending.Count == 0) return;
        var pending = _pending.ToList();
        _pending.Clear();
        foreach (var note in pending)
        {
            var story = request.Note(note.NoteId, run.ColumnWidth);
            if (story is not null && note.Atom < story.Atoms.Count) Take(note.NoteId, story, note.Atom, run.Y, run.Column, run.ColumnLeft, run.ColumnWidth);
        }
    }

    public void Reset(int columns)
    {
        _noteHeight = 0;
        _columnNotes = new double[Math.Max(1, columns)];
    }

    public void Ensure(int columns)
    {
        if (_columnNotes.Length < columns) Array.Resize(ref _columnNotes, columns);
    }

    public void Resume(IReadOnlyList<PendingNote> pending, int area, double left, double width)
    {
        foreach (var note in pending)
        {
            var story = request.Note(note.NoteId, width);
            if (story is not null && note.Atom < story.Atoms.Count) Take(note.NoteId, story, note.Atom, run.Geometry.Body.Y, area, left, width);
        }
    }

    public List<PendingNote> TakePending()
    {
        var pending = _pending.ToList();
        _pending.Clear();
        return pending;
    }

    public List<PendingNote> Withdraw()
    {
        var notes = _notes.Select(note => new PendingNote(note.NoteId, note.From)).Concat(_pending).ToList();
        _notes.Clear();
        _pending.Clear();
        return notes;
    }

    public void ClearPlaced() => _notes.Clear();

    public AreaBox? Area(double contentBottom)
    {
        if (_notes.Count == 0) return null;
        var geometry = run.Geometry;
        var content = new List<LayoutBox>();
        var bounds = new List<LayoutRect>();
        foreach (var group in _notes.GroupBy(note => note.Area))
        {
            var height = group.Key >= 0 ? _columnNotes[group.Key] : _noteHeight;
            var top = geometry.Body.Bottom - _noteHeight - (group.Key >= 0 ? height : 0);
            if (request.FootnotesBelowText && group.Key < 0) top = Math.Min(top, Math.Max(geometry.Body.Y, contentBottom));
            var left = group.First().Left;
            var width = group.First().Width;
            var rule = Rule;
            content.Add(new DecorationBox(DecorationKind.FootnoteSeparator, new LayoutRect(left, top + rule.SpaceBefore, width * rule.RelativeLength, rule.Thickness), null, rule.Thickness));
            var y = top + Separator;
            foreach (var note in group)
            {
                y += EmitNotice(content, note.Backward, left, y, width);
                var origin = note.From == 0 ? 0 : note.Story.Atoms[note.From].Top;
                var first = note.Story.Atoms[note.From].Top - origin;
                content.AddRange(stories.Emit(note.Story, note.From, note.To, left, y + first, width, StoryKind.Footnote, note.NoteId));
                y += Slice(note.Story, note.From, note.To);
                y += EmitNotice(content, note.Forward, left, y, width);
            }
            bounds.Add(new LayoutRect(left, top, width, height));
        }
        var x = bounds.Min(rect => rect.X);
        var areaTop = bounds.Min(rect => rect.Y);
        return new AreaBox(AreaKind.Footnotes, new LayoutRect(x, areaTop, bounds.Max(rect => rect.Right) - x, bounds.Max(rect => rect.Bottom) - areaTop), content);
    }

    private ComposedStory? Notice(string? text, double width) => text is { Length: > 0 } ? request.Notice(text, width) : null;

    private void Take(string noteId, ComposedStory story, int from, double contentBottom, int area, double left, double width)
    {
        var geometry = run.Geometry;
        var separator = _notes.Any(note => note.Area == area) ? 0 : Separator;
        var used = area >= 0 ? _columnNotes[area] : _noteHeight;
        var limit = geometry.Body.Height - geometry.Content.Height * options.BodyReserve - used - separator;
        var space = Math.Min(geometry.Body.Bottom - _noteHeight - (area >= 0 ? _columnNotes[area] : 0) - separator - contentBottom, limit);
        var backward = from > 0 ? Notice(request.ContinuationBackward, width) : null;
        var lead = backward?.Height ?? 0;
        var to = from;
        while (to < story.Atoms.Count && lead + Slice(story, from, to + 1) <= space + Epsilon) to++;
        to = Kept(story, from, to);
        if (to == from && contentBottom <= geometry.Body.Y + Epsilon) to = from + 1;
        if (to == from)
        {
            _pending.Add(new PendingNote(noteId, from));
            return;
        }
        var forward = to < story.Atoms.Count ? Notice(request.ContinuationForward, width) : null;
        while (forward is not null && to > from + 1 && lead + Slice(story, from, to) + forward.Height > space + Epsilon) to = Math.Max(from + 1, Kept(story, from, to - 1));
        var placed = new PlacedNote(noteId, story, from, to, area, left, width) { Backward = backward, Forward = forward };
        _notes.Add(placed);
        if (area >= 0) _columnNotes[area] += separator + placed.Height(Slice(story, from, to));
        else _noteHeight += separator + placed.Height(Slice(story, from, to));
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

    private double EmitNotice(List<LayoutBox> content, ComposedStory? notice, double left, double y, double width)
    {
        if (notice is null || notice.Atoms.Count == 0) return 0;
        content.AddRange(stories.Emit(notice, 0, notice.Atoms.Count, left, y, width, StoryKind.Footnote, null));
        return notice.Height;
    }
}
