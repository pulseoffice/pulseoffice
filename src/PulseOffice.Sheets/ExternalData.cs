using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets;

public enum ExternalDataFormat { Csv, Html, Xml }

public enum StreamInsertion { Bottom, Top }

public sealed record AreaLink
{
    public required int SheetIndex { get; init; }

    public required CellAddress Anchor { get; init; }

    public required string Url { get; init; }

    public ExternalDataFormat Format { get; init; } = ExternalDataFormat.Html;

    public string Source { get; init; } = string.Empty;

    public int Columns { get; init; } = 1;

    public int Rows { get; init; } = 1;

    public int RefreshSeconds { get; init; }

    public CellRange Area => new(Anchor, new CellAddress(
        (int)Math.Min((long)Anchor.Row + Math.Max(Rows, 1) - 1, CellAddress.MaxRows - 1),
        (int)Math.Min((long)Anchor.Column + Math.Max(Columns, 1) - 1, CellAddress.MaxColumns - 1)));
}

public sealed record DataStreamSource
{
    public required string Url { get; init; }

    public required int SheetIndex { get; init; }

    public required CellRange Range { get; init; }

    public StreamInsertion Insertion { get; init; } = StreamInsertion.Bottom;

    public bool RefreshOnEmptyLine { get; init; }
}

public sealed record DataMapping
{
    public required string Id { get; init; }

    public required string Url { get; init; }

    public ExternalDataFormat Provider { get; init; } = ExternalDataFormat.Csv;

    public required string DatabaseName { get; init; }
}

public sealed record ExternalDataMemento(
    IReadOnlyList<AreaLink> AreaLinks,
    DataStreamSource? Stream,
    IReadOnlyList<DataMapping> Mappings,
    IReadOnlyList<DatabaseRange> DatabaseRanges);

public sealed class ExternalDataCatalog
{
    public List<AreaLink> AreaLinks { get; } = [];

    public DataStreamSource? Stream { get; set; }

    public List<DataMapping> Mappings { get; } = [];

    public ExternalDataMemento Reindex(Func<int, int> map)
    {
        var detachedLinks = new List<AreaLink>();
        for (var index = AreaLinks.Count - 1; index >= 0; index--)
        {
            var link = AreaLinks[index];
            var target = map(link.SheetIndex);
            if (target < 0)
            {
                detachedLinks.Insert(0, link);
                AreaLinks.RemoveAt(index);
            }
            else if (target != link.SheetIndex) AreaLinks[index] = link with { SheetIndex = target };
        }

        DataStreamSource? detachedStream = null;
        if (Stream is { } stream)
        {
            var target = map(stream.SheetIndex);
            if (target < 0)
            {
                detachedStream = stream;
                Stream = null;
            }
            else if (target != stream.SheetIndex) Stream = stream with { SheetIndex = target };
        }

        return new ExternalDataMemento(detachedLinks, detachedStream, [], []);
    }

    public void Attach(ExternalDataMemento detached, int sheetIndex)
    {
        foreach (var link in detached.AreaLinks) Replace(link with { SheetIndex = sheetIndex });
        if (detached.Stream is { } stream && Stream is null) Stream = stream with { SheetIndex = sheetIndex };
    }

    public ExternalDataMemento Save(DatabaseRangeCollection ranges) =>
        new([.. AreaLinks], Stream, [.. Mappings], [.. ranges.All]);

    public void Restore(ExternalDataMemento memento, DatabaseRangeCollection ranges)
    {
        AreaLinks.Clear();
        AreaLinks.AddRange(memento.AreaLinks);
        Stream = memento.Stream;
        Mappings.Clear();
        Mappings.AddRange(memento.Mappings);
        ranges.ReplaceAll(memento.DatabaseRanges);
    }

    public void Shift(int sheetIndex, Func<CellRange, CellRange?> move, DatabaseRangeCollection ranges)
    {
        for (var index = AreaLinks.Count - 1; index >= 0; index--)
        {
            var link = AreaLinks[index];
            if (link.SheetIndex != sheetIndex) continue;
            if (move(link.Area) is { } area)
                AreaLinks[index] = link with { Anchor = area.Start, Rows = area.End.Row - area.Start.Row + 1, Columns = area.End.Column - area.Start.Column + 1 };
            else AreaLinks.RemoveAt(index);
        }

        if (Stream is { } stream && stream.SheetIndex == sheetIndex)
            Stream = move(stream.Range) is { } range ? stream with { Range = range } : null;

        foreach (var database in ranges.All.Where(range => range.SheetIndex == sheetIndex).ToList())
        {
            if (move(database.Range) is { } moved) ranges.Define(database with { Range = moved });
            else ranges.Remove(database.Name);
        }
    }

    public bool IsEmpty => AreaLinks.Count == 0 && Stream is null && Mappings.Count == 0;

    public void Replace(AreaLink link)
    {
        AreaLinks.RemoveAll(existing => existing.SheetIndex == link.SheetIndex && existing.Anchor == link.Anchor);
        AreaLinks.Add(link);
    }

    public void Replace(DataMapping mapping)
    {
        Mappings.RemoveAll(existing => string.Equals(existing.DatabaseName, mapping.DatabaseName, StringComparison.OrdinalIgnoreCase));
        Mappings.Add(mapping);
    }
}
