namespace PulseOffice.Documents.Fields;

public sealed class BibliographySource(string identifier, string type, IReadOnlyDictionary<string, string> values)
{
    public string Identifier { get; } = identifier;

    public string Type { get; } = type;

    public IReadOnlyDictionary<string, string> Values { get; } = values;

    public string Value(string field) => Values.TryGetValue(field, out var value) ? value : string.Empty;

    public bool SameAs(BibliographySource other) =>
        Identifier == other.Identifier && Type == other.Type && Values.Count == other.Values.Count
        && Values.All(pair => other.Values.TryGetValue(pair.Key, out var value) && value == pair.Value);
}

public sealed class BibliographyFieldNames
{
    public IReadOnlyList<string> All { get; } =
    [
        "author", "title", "year", "publisher", "address", "editor", "edition", "booktitle", "journal",
        "volume", "number", "pages", "chapter", "series", "school", "institution", "organizations",
        "howpublished", "month", "note", "annote", "report-type", "isbn", "url",
        "custom1", "custom2", "custom3", "custom4", "custom5",
    ];

    public IReadOnlyList<string> Types { get; } =
    [
        "article", "book", "booklet", "conference", "custom1", "custom2", "custom3", "custom4", "custom5",
        "email", "inbook", "incollection", "inproceedings", "journal", "manual", "mastersthesis", "misc",
        "phdthesis", "proceedings", "techreport", "unpublished", "www",
    ];
}

public sealed class BibliographyCollection
{
    private readonly List<BibliographySource> _sources = [];

    public IReadOnlyList<BibliographySource> All => _sources;

    public BibliographySource? Find(string identifier) =>
        _sources.FirstOrDefault(source => string.Equals(source.Identifier, identifier, StringComparison.Ordinal));

    public void Store(BibliographySource source)
    {
        var index = _sources.FindIndex(existing => existing.Identifier == source.Identifier);
        if (index >= 0) _sources[index] = source;
        else _sources.Add(source);
    }

    public bool Remove(string identifier) => _sources.RemoveAll(source => source.Identifier == identifier) > 0;

    public void Replace(IEnumerable<BibliographySource> sources)
    {
        var copy = sources.ToList();
        _sources.Clear();
        _sources.AddRange(copy);
    }

    public void CopyFrom(BibliographyCollection other) => Replace(other._sources);
}
