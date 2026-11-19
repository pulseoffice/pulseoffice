using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Layout.Flow;

public enum TableMeasureKind
{
    Optimal,
    Minimal,
}

public interface ITableMeasure
{
    IReadOnlyList<double> Widths(TextDocument document, TableBlock table, TableMeasureKind kind);
}

public sealed class TableMeasure(IParagraphEntries entries, IBlockTree tree, ITableAppearance appearance, PageFlowOptions options) : ITableMeasure
{
    private const double Unbounded = 100_000;

    public IReadOnlyList<double> Widths(TextDocument document, TableBlock table, TableMeasureKind kind)
    {
        var style = appearance.Style(document.Styles, table);
        var inset = 2 * (style.CellPadding ?? options.TableCellPadding) + (style.BorderWidth ?? 0);
        var widths = table.Columns.Select(column => column.ToPoints()).ToList();
        var wanted = new double?[widths.Count];
        var spanning = new List<(int Column, int Span, double Natural)>();
        foreach (var row in table.Rows)
        {
            for (var column = 0; column < row.Cells.Count && column < widths.Count; column++)
            {
                var cell = row.Cells[column];
                if (cell.Covered) continue;
                var natural = Content(document, cell.Content, kind) + inset;
                if (cell.ColumnSpan > 1) spanning.Add((column, Math.Min(cell.ColumnSpan, widths.Count - column), natural));
                else wanted[column] = Math.Max(wanted[column] ?? 0, natural);
            }
        }
        var result = widths.Select((width, column) => wanted[column] ?? width).ToList();
        foreach (var (column, span, natural) in spanning.OrderBy(entry => entry.Span)) Spread(result, column, span, natural);
        return result;
    }

    private void Spread(List<double> widths, int column, int span, double natural)
    {
        var covered = Enumerable.Range(column, span).Sum(index => widths[index]);
        var excess = natural - covered;
        if (excess <= 0) return;
        for (var index = column; index < column + span; index++)
            widths[index] += covered > 0 ? excess * widths[index] / covered : excess / span;
    }

    private double Content(TextDocument document, BlockContainer content, TableMeasureKind kind) =>
        content.Blocks.Select(block => block switch
        {
            ParagraphBlock paragraph => Natural(document, paragraph, kind),
            TableBlock nested => nested.AutoWidth || nested.WidthPercent is > 0 ? Widths(document, nested, kind).Sum() : nested.Columns.Sum(width => width.ToPoints()),
            _ => tree.Paragraphs(new BlockContainer([block])).Select(paragraph => Natural(document, paragraph, kind)).DefaultIfEmpty(0).Max(),
        }).DefaultIfEmpty(0).Max();

    private double Natural(TextDocument document, ParagraphBlock paragraph, TableMeasureKind kind)
    {
        if (paragraph.Length == 0) return 0;
        var entry = entries.Entry(document, new EntryRequest(paragraph, paragraph.Content, null));
        var lines = entry.Lines(kind == TableMeasureKind.Optimal ? Unbounded : 1);
        return lines.Select(line => line.Runs.Select(run => run.X + run.Advances.Sum()).DefaultIfEmpty(0).Max()).DefaultIfEmpty(0).Max();
    }
}
