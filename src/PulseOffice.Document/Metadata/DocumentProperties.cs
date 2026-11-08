namespace PulseOffice.Document.Metadata;

public enum CustomPropertyKind { Text, Number, Boolean, Date }

public sealed class DocumentProperties
{
    public string? Title { get; set; }

    public string? Subject { get; set; }

    public string? Description { get; set; }

    public string? Author { get; set; }

    public string? LastModifiedBy { get; set; }

    public List<string> Keywords { get; } = [];

    public DateTimeOffset? Created { get; set; }

    public DateTimeOffset? Modified { get; set; }

    public string? Generator { get; set; }

    public string? Language { get; set; }

    public int EditingCycles { get; set; }

    public Dictionary<string, string> Custom { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, CustomPropertyKind> CustomKinds { get; } = new(StringComparer.Ordinal);

    public CustomPropertyKind KindOf(string name) => CustomKinds.GetValueOrDefault(name, CustomPropertyKind.Text);

    public bool IsEmpty =>
        Title is null && Subject is null && Description is null && Author is null && Language is null
        && Keywords.Count == 0 && Custom.Count == 0;

    public void CopyFrom(DocumentProperties other)
    {
        ArgumentNullException.ThrowIfNull(other);

        Title = other.Title;
        Subject = other.Subject;
        Description = other.Description;
        Author = other.Author;
        LastModifiedBy = other.LastModifiedBy;
        Created = other.Created;
        Modified = other.Modified;
        Generator = other.Generator;
        Language = other.Language;
        EditingCycles = other.EditingCycles;

        Keywords.Clear();
        Keywords.AddRange(other.Keywords);

        Custom.Clear();
        foreach (var (key, value) in other.Custom) Custom[key] = value;

        CustomKinds.Clear();
        foreach (var (key, kind) in other.CustomKinds) CustomKinds[key] = kind;
    }

    public void MarkSaved(string generator, string? author)
    {
        Generator = generator;
        Modified = DateTimeOffset.Now;
        Created ??= Modified;
        EditingCycles++;

        if (author is { Length: > 0 })
        {
            Author ??= author;
            LastModifiedBy = author;
        }
    }
}
