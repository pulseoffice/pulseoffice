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
        foreach (var row in table.Rows)
        {
            for (var column = 0; column < row.Cells.Count && column < widths.Count; column++)
            {
                var cell = row.Cells[column];
                if (cell.Covered || cell.ColumnSpan > 1) continue;
                var natural = tree.Paragraphs(cell.Content).Select(paragraph => Natural(document, paragraph, kind)).DefaultIfEmpty(0).Max();
                wanted[column] = Math.Max(wanted[column] ?? 0, natural + inset);
            }
        }
        return [.. widths.Select((width, column) => wanted[column] ?? width)];
    }

    private double Natural(TextDocument document, ParagraphBlock paragraph, TableMeasureKind kind)
    {
        if (paragraph.Length == 0) return 0;
        var entry = entries.Entry(document, new EntryRequest(paragraph, paragraph.Content, null));
        var lines = entry.Lines(kind == TableMeasureKind.Optimal ? Unbounded : 1);
        return lines.Select(line => line.Runs.Select(run => run.X + run.Advances.Sum()).DefaultIfEmpty(0).Max()).DefaultIfEmpty(0).Max();
    }
}
