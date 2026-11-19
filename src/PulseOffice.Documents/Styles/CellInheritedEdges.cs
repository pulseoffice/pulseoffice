using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Styles;

public interface ICellInheritedEdges
{
    void SuppressInherited(TableBlock table, Func<TableCell, BorderSide, bool> isExplicit);
}

public sealed class CellInheritedEdges : ICellInheritedEdges
{
    public void SuppressInherited(TableBlock table, Func<TableCell, BorderSide, bool> isExplicit)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(isExplicit);
        for (var row = 0; row < table.Rows.Count; row++)
            for (var column = 0; column < table.Rows[row].Cells.Count; column++)
            {
                var cell = table.Rows[row].Cells[column];
                if (cell.Covered) continue;
                var rows = Math.Max(1, cell.RowSpan);
                var columns = Math.Max(1, cell.ColumnSpan);
                for (var r = row; r < Math.Min(table.Rows.Count, row + rows); r++)
                    if (Neighbour(table, r, column + columns) is { } right) Settle(cell, BorderSide.Right, right, BorderSide.Left, isExplicit);
                for (var c = column; c < column + columns; c++)
                    if (Neighbour(table, row + rows, c) is { } below) Settle(cell, BorderSide.Bottom, below, BorderSide.Top, isExplicit);
            }
    }

    private TableCell? Neighbour(TableBlock table, int row, int column) =>
        row < table.Rows.Count && column < table.Rows[row].Cells.Count && table.Rows[row].Cells[column] is { Covered: false } cell ? cell : null;

    private void Settle(TableCell first, BorderSide firstSide, TableCell second, BorderSide secondSide, Func<TableCell, BorderSide, bool> isExplicit)
    {
        var firstExplicit = isExplicit(first, firstSide);
        var secondExplicit = isExplicit(second, secondSide);
        if (firstExplicit && !secondExplicit) Suppress(first, firstSide, second, secondSide);
        else if (secondExplicit && !firstExplicit) Suppress(second, secondSide, first, firstSide);
    }

    private void Suppress(TableCell owner, BorderSide ownerSide, TableCell neighbour, BorderSide neighbourSide)
    {
        if (owner.Borders?.Line(ownerSide) is not null || neighbour.Borders is not { } borders || borders.Line(neighbourSide) is null) return;
        neighbour.Borders = borders.With(neighbourSide, null, borders.Padding(neighbourSide));
    }
}
