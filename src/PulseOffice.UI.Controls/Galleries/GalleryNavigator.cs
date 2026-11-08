namespace PulseOffice.UI.Controls.Galleries;

public interface IGalleryNavigator
{
    int Target(IReadOnlyList<int> sectionSizes, int columns, int current, GalleryMove move);

    IReadOnlyList<GalleryRow> Rows(IReadOnlyList<GallerySection> sections, int columns);
}

public sealed class GalleryNavigator : IGalleryNavigator
{
    public int Target(IReadOnlyList<int> sectionSizes, int columns, int current, GalleryMove move)
    {
        ArgumentNullException.ThrowIfNull(sectionSizes);
        var count = sectionSizes.Sum();
        if (count == 0) return -1;
        if (current < 0 || current >= count) return move == GalleryMove.Last ? count - 1 : 0;
        columns = Math.Max(1, columns);

        var (section, start) = Locate(sectionSizes, current);
        var offset = current - start;
        var size = sectionSizes[section];
        var column = offset % columns;
        var row = offset / columns;
        var lastRow = (size - 1) / columns;

        return move switch
        {
            GalleryMove.Left => Math.Max(0, current - 1),
            GalleryMove.Right => Math.Min(count - 1, current + 1),
            GalleryMove.First => 0,
            GalleryMove.Last => count - 1,
            GalleryMove.Up when row > 0 => current - columns,
            GalleryMove.Up when section > 0 => LastRowOf(sectionSizes, section - 1, start, columns, column),
            GalleryMove.Up => 0,
            GalleryMove.Down when row < lastRow => start + Math.Min(size - 1, offset + columns),
            GalleryMove.Down when section < sectionSizes.Count - 1 => NextFirstRow(sectionSizes, section + 1, start + size, columns, column),
            GalleryMove.Down => count - 1,
            _ => current,
        };
    }

    public IReadOnlyList<GalleryRow> Rows(IReadOnlyList<GallerySection> sections, int columns)
    {
        ArgumentNullException.ThrowIfNull(sections);
        columns = Math.Max(1, columns);
        var rows = new List<GalleryRow>();
        foreach (var section in sections)
        {
            if (section.Header is not null) rows.Add(new GalleryRow(section.Header, []));
            rows.AddRange(section.Positions.Chunk(columns).Select(chunk => new GalleryRow(null, chunk)));
        }
        return rows;
    }

    private (int Section, int Start) Locate(IReadOnlyList<int> sizes, int position)
    {
        var start = 0;
        for (var i = 0; i < sizes.Count; i++)
        {
            if (position < start + sizes[i]) return (i, start);
            start += sizes[i];
        }
        return (sizes.Count - 1, start - sizes[^1]);
    }

    private int LastRowOf(IReadOnlyList<int> sizes, int section, int nextStart, int columns, int column)
    {
        var size = sizes[section];
        var start = nextStart - size;
        var rowStart = (size - 1) / columns * columns;
        return start + Math.Min(size - 1, rowStart + column);
    }

    private int NextFirstRow(IReadOnlyList<int> sizes, int section, int start, int columns, int column) =>
        start + Math.Min(Math.Min(sizes[section], columns) - 1, column);
}
