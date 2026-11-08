using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Editing.Tables;

public sealed record SpanHeir(int Column, TableCell Cell);

public interface ITableSpanCutting
{
    IReadOnlyList<SpanHeir> CutAbove(TextDocument document, TableBlock table, int row);

    TableCell? CutBefore(TextDocument document, List<TableCell> cells, int column);
}

public sealed class TableSpanCutting : ITableSpanCutting
{
    public IReadOnlyList<SpanHeir> CutAbove(TextDocument document, TableBlock table, int row)
    {
        var heirs = new List<SpanHeir>();
        for (var r = 0; r < row && r < table.Rows.Count; r++)
        {
            var cells = table.Rows[r].Cells;
            for (var c = 0; c < cells.Count; c++)
            {
                var origin = cells[c];
                if (origin.Covered || r + origin.RowSpan <= row) continue;
                heirs.Add(new SpanHeir(c, Heir(document, origin, origin.ColumnSpan, r + origin.RowSpan - row)));
                origin.RowSpan = row - r;
            }
        }
        return heirs;
    }

    public TableCell? CutBefore(TextDocument document, List<TableCell> cells, int column)
    {
        for (var c = 0; c < column && c < cells.Count; c++)
        {
            var origin = cells[c];
            if (origin.Covered || c + origin.ColumnSpan <= column) continue;
            var heir = Heir(document, origin, c + origin.ColumnSpan - column, origin.RowSpan);
            origin.ColumnSpan = column - c;
            return heir;
        }
        return null;
    }

    private TableCell Heir(TextDocument document, TableCell origin, int columnSpan, int rowSpan)
    {
        var heir = origin.Clone();
        heir.Content.Replace([new ParagraphBlock(document.NextId("p"), string.Empty)]);
        heir.ColumnSpan = columnSpan;
        heir.RowSpan = rowSpan;
        heir.Covered = false;
        heir.Formula = null;
        return heir;
    }
}
