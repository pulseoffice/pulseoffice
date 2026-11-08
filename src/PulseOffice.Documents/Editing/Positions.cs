namespace PulseOffice.Documents.Editing;

public readonly record struct DocumentPosition(string ParagraphId, int Offset);

public readonly record struct DocumentRange(DocumentPosition Start, DocumentPosition End)
{
    public static DocumentRange At(DocumentPosition position) => new(position, position);

    public static DocumentRange In(string paragraphId, int start, int end) => new(new(paragraphId, start), new(paragraphId, end));

    public bool IsCollapsed => Start == End;
}

public abstract record TextChange
{
    public abstract TextChange Inverse();

    public abstract DocumentPosition Map(DocumentPosition position);
}

public sealed record TextInserted(string ParagraphId, int Offset, int Length) : TextChange
{
    public override TextChange Inverse() => new TextDeleted(ParagraphId, Offset, Length);

    public override DocumentPosition Map(DocumentPosition position) =>
        position.ParagraphId == ParagraphId && position.Offset >= Offset ? position with { Offset = position.Offset + Length } : position;
}

public sealed record TextDeleted(string ParagraphId, int Offset, int Length) : TextChange
{
    public override TextChange Inverse() => new TextInserted(ParagraphId, Offset, Length);

    public override DocumentPosition Map(DocumentPosition position)
    {
        if (position.ParagraphId != ParagraphId || position.Offset <= Offset) return position;
        return position with { Offset = position.Offset >= Offset + Length ? position.Offset - Length : Offset };
    }
}

public sealed record ParagraphSplit(string ParagraphId, int Offset, string NewParagraphId) : TextChange
{
    public override TextChange Inverse() => new ParagraphsMerged(ParagraphId, NewParagraphId, Offset);

    public override DocumentPosition Map(DocumentPosition position) =>
        position.ParagraphId == ParagraphId && position.Offset >= Offset
            ? new DocumentPosition(NewParagraphId, position.Offset - Offset)
            : position;
}

public sealed record ParagraphsMerged(string TargetId, string SourceId, int JoinOffset) : TextChange
{
    public override TextChange Inverse() => new ParagraphSplit(TargetId, JoinOffset, SourceId);

    public override DocumentPosition Map(DocumentPosition position) =>
        position.ParagraphId == SourceId ? new DocumentPosition(TargetId, JoinOffset + position.Offset) : position;
}

public sealed record BlockRemoved(string BlockId, DocumentPosition Fallback, IReadOnlyList<string> ParagraphIds) : TextChange
{
    public override TextChange Inverse() => new BlockRestored(BlockId);

    public override DocumentPosition Map(DocumentPosition position) =>
        ParagraphIds.Contains(position.ParagraphId) ? Fallback : position;
}

public sealed record BlockRestored(string BlockId) : TextChange
{
    public override TextChange Inverse() => this;

    public override DocumentPosition Map(DocumentPosition position) => position;
}

public sealed class TextChangedEventArgs(IReadOnlyList<TextChange> changes) : EventArgs
{
    public IReadOnlyList<TextChange> Changes { get; } = changes;
}

public sealed class TrackedPosition(DocumentPosition position)
{
    public DocumentPosition Position { get; set; } = position;
}

public interface IPositionTracker : IDisposable
{
    TrackedPosition Track(DocumentPosition position);

    void Release(TrackedPosition position);
}

public sealed class PositionTracker : IPositionTracker
{
    private readonly TextDocument _document;
    private readonly List<TrackedPosition> _positions = [];

    public PositionTracker(TextDocument document)
    {
        _document = document;
        _document.TextChanged += OnTextChanged;
    }

    public TrackedPosition Track(DocumentPosition position)
    {
        var tracked = new TrackedPosition(position);
        _positions.Add(tracked);
        return tracked;
    }

    public void Release(TrackedPosition position) => _positions.Remove(position);

    public void Dispose() => _document.TextChanged -= OnTextChanged;

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        foreach (var tracked in _positions)
        {
            tracked.Position = e.Changes.Aggregate(tracked.Position, (position, change) => change.Map(position));
        }
    }
}

public interface IPositionTrackerFactory
{
    IPositionTracker Create(TextDocument document);
}

public sealed class PositionTrackerFactory : IPositionTrackerFactory
{
    public IPositionTracker Create(TextDocument document) => new PositionTracker(document);
}
