using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Styling;

public sealed record StyleBandChange(StyleBandsMemento Before, StyleBandsMemento After)
{
    public bool Changed => !Before.SameAs(After);
}

public interface IStyleBandFormatter
{
    StyleBandChange Apply(SheetLayout layout, CellRange selection, SelectionWork work, Func<CellAddress, int, int> restyle);

    IEnumerable<int> DefaultStyles(SheetLayout layout, CellRange selection, SelectionWork work);
}

public sealed class StyleBandFormatter : IStyleBandFormatter
{
    public StyleBandChange Apply(SheetLayout layout, CellRange selection, SelectionWork work, Func<CellAddress, int, int> restyle)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(restyle);

        var bands = layout.Styles;
        var before = bands.Save();
        if (!work.SetsDefaults) return new StyleBandChange(before, before);

        var crossings = Crossings(bands, selection, work)
            .Select(piece => (Piece: piece, Target: restyle(piece.Start, bands.StyleAt(piece.Start))))
            .ToList();

        var interiorRow = Math.Min(selection.Start.Row + 1, selection.End.Row);
        var interiorColumn = Math.Min(selection.Start.Column + 1, selection.End.Column);
        var columns = LineChanges(work.Columns, selection.Start.Column, selection.End.Column, bands.Columns,
            column => new CellAddress(interiorRow, column), restyle, presentOnly: false);
        var rows = LineChanges(work.Rows, selection.Start.Row, selection.End.Row, bands.Rows,
            row => new CellAddress(row, interiorColumn), restyle, presentOnly: WholeSheet(selection));

        foreach (var (first, last, style) in columns) bands.SetColumns(first, last, style);
        foreach (var (first, last, style) in rows) bands.SetRows(first, last, style);

        foreach (var (piece, target) in crossings)
        {
            if (bands.LineStyleAt(piece.Start) == target)
                bands.Blocks.Remove(piece.Start.Row, piece.End.Row, piece.Start.Column, piece.End.Column);
            else bands.SetBlock(piece.Start.Row, piece.End.Row, piece.Start.Column, piece.End.Column, target);
        }

        return new StyleBandChange(before, bands.Save());
    }

    public IEnumerable<int> DefaultStyles(SheetLayout layout, CellRange selection, SelectionWork work)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(work);

        var bands = layout.Styles;
        return work.Columns.SelectMany(span => bands.Columns.Segments(span.First, span.Last))
            .Select(segment => segment.Present ? segment.Value : StyleCatalog.DefaultStyleId)
            .Concat(work.Rows.SelectMany(span => bands.Rows.Segments(span.First, span.Last))
                .Where(segment => segment.Present || !WholeSheet(selection))
                .Select(segment => segment.Present ? segment.Value : StyleCatalog.DefaultStyleId))
            .Concat(work.SetsDefaults ? Crossings(bands, selection, work).Select(piece => bands.StyleAt(piece.Start)) : []);
    }

    private List<CellRange> Crossings(StyleBands bands, CellRange selection, SelectionWork work)
    {
        var wholeRows = work.Rows.Count > 0 && WholeRows(selection);
        var wholeColumns = work.Columns.Count > 0 && WholeColumns(selection);
        var result = new List<CellRange>();
        if (!wholeRows && !wholeColumns && bands.Blocks.IsEmpty) return result;

        IReadOnlyList<IndexSpan> rowSpans = wholeColumns ? [new IndexSpan(selection.Start.Row, selection.End.Row)] : work.Rows;
        IReadOnlyList<IndexSpan> columnSpans = wholeRows ? [new IndexSpan(selection.Start.Column, selection.End.Column)] : work.Columns;
        var rowCuts = bands.Rows.Runs.SelectMany(run => new[] { run.First, run.Last + 1 }).Concat(bands.Blocks.RowEdges)
            .Concat(Edges(selection.Start.Row, selection.End.Row));
        var columnCuts = bands.Columns.Runs.SelectMany(run => new[] { run.First, run.Last + 1 }).Concat(bands.Blocks.ColumnEdges)
            .Concat(Edges(selection.Start.Column, selection.End.Column));
        var rowPieces = rowSpans.SelectMany(span => Pieces(span, rowCuts)).ToList();
        var columnPieces = columnSpans.SelectMany(span => Pieces(span, columnCuts)).ToList();

        foreach (var columnPiece in columnPieces)
        {
            var columnStyled = bands.Columns.ContainsKey(columnPiece.First);
            foreach (var rowPiece in rowPieces)
            {
                var start = new CellAddress(rowPiece.First, columnPiece.First);
                var crossing = bands.Blocks.TryGetValue(start.Row, start.Column, out _)
                    || (wholeRows && !wholeColumns && columnStyled)
                    || (wholeColumns && !wholeRows && bands.Rows.ContainsKey(start.Row));
                if (crossing) result.Add(new CellRange(start, new CellAddress(rowPiece.Last, columnPiece.Last)));
            }
        }

        return result;
    }

    private List<(int First, int Last, int? Style)> LineChanges(
        IReadOnlyList<IndexSpan> spans, int edgeStart, int edgeEnd, RunLengthMap<int> styles, Func<int, CellAddress> probe,
        Func<CellAddress, int, int> restyle, bool presentOnly)
    {
        var result = new List<(int First, int Last, int? Style)>();
        foreach (var span in spans)
            foreach (var piece in Pieces(span, Edges(edgeStart, edgeEnd)))
                foreach (var segment in styles.Segments(piece.First, piece.Last).Where(segment => segment.Present || !presentOnly))
                {
                    int? before = segment.Present ? segment.Value : null;
                    var after = restyle(probe(segment.First), before ?? StyleCatalog.DefaultStyleId);
                    if (before != after) result.Add((segment.First, segment.Last, after));
                }

        return result;
    }

    private bool WholeRows(CellRange selection) =>
        selection.Start.Column == 0 && selection.End.Column >= CellAddress.MaxColumns - 1;

    private bool WholeColumns(CellRange selection) =>
        selection.Start.Row == 0 && selection.End.Row >= CellAddress.MaxRows - 1;

    private bool WholeSheet(CellRange selection) => WholeRows(selection) && WholeColumns(selection);

    private IEnumerable<int> Edges(int start, int end) => [start, start + 1, end, end + 1];

    private IEnumerable<IndexSpan> Pieces(IndexSpan span, IEnumerable<int> cuts)
    {
        var bounds = new SortedSet<int> { span.First, span.Last + 1 };
        foreach (var cut in cuts)
            if (cut > span.First && cut <= span.Last) bounds.Add(cut);

        var ordered = bounds.ToList();
        for (var index = 0; index + 1 < ordered.Count; index++)
            yield return new IndexSpan(ordered[index], ordered[index + 1] - 1);
    }
}
