using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Annotations;

public enum ChangeKind
{
    Insertion,
    Deletion,
    Format,
    ParagraphFormat,
}

public abstract class TrackedChange(string id, string author, DateTimeOffset date)
{
    public string Id { get; } = id;

    public string Author { get; set; } = author;

    public DateTimeOffset Date { get; set; } = date;

    public string? Comment { get; set; }

    public abstract ChangeKind Kind { get; }

    public abstract TrackedChange Clone();

    public abstract TrackedChange CopyAs(string id, IReadOnlyDictionary<string, string> renamed, Func<BlockContainer, BlockContainer> content);
}

public sealed class Insertion(string id, string author, DateTimeOffset date) : TrackedChange(id, author, date)
{
    public override ChangeKind Kind => ChangeKind.Insertion;

    public override TrackedChange Clone() => new Insertion(Id, Author, Date) { Comment = Comment };

    public override TrackedChange CopyAs(string id, IReadOnlyDictionary<string, string> renamed, Func<BlockContainer, BlockContainer> content) => new Insertion(id, Author, Date) { Comment = Comment };
}

public sealed class Deletion(string id, string author, DateTimeOffset date, BlockContainer removed)
    : TrackedChange(id, author, date)
{
    public BlockContainer Removed { get; } = removed;

    public string? WithinInsertion { get; init; }

    public override ChangeKind Kind => ChangeKind.Deletion;

    public override TrackedChange Clone() => new Deletion(Id, Author, Date, Removed.Clone()) { Comment = Comment, WithinInsertion = WithinInsertion };

    public override TrackedChange CopyAs(string id, IReadOnlyDictionary<string, string> renamed, Func<BlockContainer, BlockContainer> content) =>
        new Deletion(id, Author, Date, content(Removed))
        {
            Comment = Comment,
            WithinInsertion = WithinInsertion is { } within && renamed.TryGetValue(within, out var insertion) ? insertion : null,
        };
}

public sealed class FormatChange(string id, string author, DateTimeOffset date, IReadOnlyList<TextRun> previousRuns)
    : TrackedChange(id, author, date)
{
    public IReadOnlyList<TextRun> PreviousRuns { get; } = previousRuns;

    public bool PreviousKnown => PreviousRuns.Count > 0;

    public override ChangeKind Kind => ChangeKind.Format;

    public override TrackedChange Clone() => new FormatChange(Id, Author, Date, PreviousRuns) { Comment = Comment };

    public override TrackedChange CopyAs(string id, IReadOnlyDictionary<string, string> renamed, Func<BlockContainer, BlockContainer> content) => new FormatChange(id, Author, Date, PreviousRuns) { Comment = Comment };
}

public sealed class ParagraphFormatChange(string id, string author, DateTimeOffset date, TextParagraph previous)
    : TrackedChange(id, author, date)
{
    public TextParagraph Previous { get; } = previous;

    public bool PreviousKnown { get; init; } = true;

    public override ChangeKind Kind => ChangeKind.ParagraphFormat;

    public override TrackedChange Clone() => new ParagraphFormatChange(Id, Author, Date, Previous) { Comment = Comment, PreviousKnown = PreviousKnown };

    public override TrackedChange CopyAs(string id, IReadOnlyDictionary<string, string> renamed, Func<BlockContainer, BlockContainer> content) =>
        new ParagraphFormatChange(id, Author, Date, Previous) { Comment = Comment, PreviousKnown = PreviousKnown };
}

public sealed class TrackedChangeLog
{
    private readonly List<TrackedChange> _changes = [];

    public void Replace(IEnumerable<TrackedChange> items)
    {
        var copy = items.ToList();
        _changes.Clear();
        _changes.AddRange(copy);
    }

    public IReadOnlyList<TrackedChange> All => _changes;

    public bool IsRecording { get; set; }

    public byte[]? ProtectionKey { get; set; }

    public bool ShowChanges { get; set; } = true;

    public TrackedChange? Find(string id) => _changes.FirstOrDefault(change => change.Id == id);

    public void Add(TrackedChange change) => _changes.Add(change);

    public bool Remove(string id) => _changes.RemoveAll(change => change.Id == id) > 0;

    public IEnumerable<TrackedChange> By(string author) => _changes.Where(change => change.Author == author);

    public void CopyFrom(TrackedChangeLog other)
    {
        _changes.Clear();
        _changes.AddRange(other._changes.Select(change => change.Clone()));
        IsRecording = other.IsRecording;
        ProtectionKey = other.ProtectionKey;
        ShowChanges = other.ShowChanges;
    }
}
