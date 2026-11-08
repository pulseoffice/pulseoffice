using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Annotations;

public enum NoteKind
{
    Footnote,
    Endnote,
}

public sealed class Note(string id, NoteKind kind, BlockContainer content)
{
    public string Id { get; } = id;

    public NoteKind Kind { get; set; } = kind;

    public BlockContainer Content { get; } = content;

    public string? Citation { get; set; }

    public Note Clone() => new(Id, Kind, Content.Clone()) { Citation = Citation };
}

public enum NoteNumbering
{
    Document,
    Chapter,
    Page,
}

public sealed record NoteSettings
{
    public string FormatName { get; init; } = "1";

    public int StartAt { get; init; } = 1;

    public NoteNumbering Restart { get; init; } = NoteNumbering.Document;

    public string? CitationStyleName { get; init; }

    public string? ParagraphStyleName { get; init; }
}

public sealed class NoteCollection
{
    private readonly List<Note> _notes = [];

    private Dictionary<string, Note>? _byId;

    public void Replace(IEnumerable<Note> items)
    {
        var copy = items.ToList();
        _notes.Clear();
        _notes.AddRange(copy);
        _byId = null;
    }

    public IReadOnlyList<Note> All => _notes;

    public NoteSettings FootnoteSettings { get; set; } = new() { ParagraphStyleName = "Footnote" };

    public NoteSettings EndnoteSettings { get; set; } = new() { FormatName = "i", ParagraphStyleName = "Endnote" };

    public bool ContinuousEndnotes { get; set; }

    public Note? Find(string id)
    {
        if (_byId is null)
        {
            _byId = new Dictionary<string, Note>(StringComparer.Ordinal);
            foreach (var note in _notes) _byId.TryAdd(note.Id, note);
        }
        return _byId.GetValueOrDefault(id);
    }

    public void Add(Note note)
    {
        _notes.Add(note);
        _byId = null;
    }

    public bool Remove(string id)
    {
        _byId = null;
        return _notes.RemoveAll(note => note.Id == id) > 0;
    }

    public IEnumerable<Note> OfKind(NoteKind kind) => _notes.Where(note => note.Kind == kind);

    public void CopyFrom(NoteCollection other)
    {
        _notes.Clear();
        _notes.AddRange(other._notes.Select(note => note.Clone()));
        _byId = null;
        FootnoteSettings = other.FootnoteSettings;
        EndnoteSettings = other.EndnoteSettings;
        ContinuousEndnotes = other.ContinuousEndnotes;
    }
}
