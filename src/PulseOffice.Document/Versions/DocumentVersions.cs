namespace PulseOffice.Document.Versions;

public sealed record DocumentVersion
{
    public required string Name { get; init; }

    public string? Author { get; init; }

    public string Comment { get; init; } = string.Empty;

    public DateTimeOffset Created { get; init; } = DateTimeOffset.Now;

    public required byte[] Content { get; init; }

    public long Size => Content.LongLength;
}

public sealed class DocumentVersionCollection
{
    private readonly List<DocumentVersion> _versions = [];

    public IReadOnlyList<DocumentVersion> All => _versions;

    public int Count => _versions.Count;

    public DocumentVersion Add(byte[] content, string? author, string comment)
    {
        ArgumentNullException.ThrowIfNull(content);

        var version = new DocumentVersion
        {
            Name = NextName(),
            Author = author,
            Comment = comment,
            Content = content,
        };

        _versions.Add(version);

        return version;
    }

    public void Adopt(DocumentVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        _versions.Add(version);
    }

    public void Restore(int index, DocumentVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (Find(version.Name) is not null) return;

        _versions.Insert(Math.Clamp(index, 0, _versions.Count), version);
    }

    public int IndexOf(string name) => _versions.FindIndex(version => version.Name == name);

    public bool Remove(string name) => _versions.RemoveAll(version => version.Name == name) > 0;

    public DocumentVersion? Find(string name) =>
        _versions.FirstOrDefault(version => version.Name == name);

    public void Clear() => _versions.Clear();

    public void Replace(IEnumerable<DocumentVersion> versions)
    {
        ArgumentNullException.ThrowIfNull(versions);

        var kept = versions.ToList();
        _versions.Clear();
        _versions.AddRange(kept);
    }

    private string NextName()
    {
        var index = _versions.Count + 1;

        while (_versions.Any(version => version.Name == $"Version{index}")) index++;

        return $"Version{index}";
    }
}
