using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Blocks;

public interface ITableGridRepair
{
    void Repair(TextDocument document, TableBlock table);
}

public sealed class TableGridRepair : ITableGridRepair
{
    public void Repair(TextDocument document, TableBlock table)
    {
        var width = Math.Max(table.Columns.Count, table.Rows.Select(row => row.Cells.Count).DefaultIfEmpty(0).Max());
        foreach (var row in table.Rows)
            while (row.Cells.Count < width) row.Cells.Add(new TableCell(new BlockContainer()) { Covered = true });
        var owned = table.Rows.Select(_ => new bool[width]).ToArray();
        for (var r = 0; r < table.Rows.Count; r++)
        {
            for (var c = 0; c < width; c++)
            {
                var cell = table.Rows[r].Cells[c];
                if (cell.Covered) continue;
                owned[r][c] = true;
                var columns = Columns(table, owned, r, c, Math.Max(1, cell.ColumnSpan));
                var rows = Rows(table, owned, r, c, columns, Math.Max(1, cell.RowSpan));
                cell.ColumnSpan = columns;
                cell.RowSpan = rows;
                for (var y = r; y < r + rows; y++)
                    for (var x = c; x < c + columns; x++)
                    {
                        owned[y][x] = true;
                        if (y != r || x != c) AbsorbCovered(cell, table.Rows[y].Cells[x]);
                    }
            }
        }
        for (var r = 0; r < table.Rows.Count; r++)
        {
            for (var c = 0; c < width; c++)
            {
                if (owned[r][c]) continue;
                var cell = table.Rows[r].Cells[c];
                cell.Covered = false;
                cell.ColumnSpan = 1;
                cell.RowSpan = 1;
                if (cell.Content.Count == 0) cell.Content.Add(new ParagraphBlock(document.NextId("p"), string.Empty, DefaultStyleNames.Standard));
                owned[r][c] = true;
            }
        }
    }

    private void AbsorbCovered(TableCell owner, TableCell covered)
    {
        if (!HasMeaningfulContent(covered.Content)) return;
        if (!HasMeaningfulContent(owner.Content)) owner.Content.Replace([]);
        foreach (var block in covered.Content.Blocks) owner.Content.Add(block);
        covered.Content.Replace([]);
    }

    private bool HasMeaningfulContent(BlockContainer content) =>
        content.Blocks.Any(block => block is not ParagraphBlock paragraph || paragraph.Text.Length > 0);

    private int Columns(TableBlock table, bool[][] owned, int row, int column, int wanted)
    {
        var span = 1;
        while (span < wanted && column + span < owned[row].Length && Free(table, owned, row, column + span)) span++;
        return span;
    }

    private int Rows(TableBlock table, bool[][] owned, int row, int column, int columns, int wanted)
    {
        var span = 1;
        while (span < wanted && row + span < table.Rows.Count && Enumerable.Range(column, columns).All(x => Free(table, owned, row + span, x))) span++;
        return span;
    }

    private bool Free(TableBlock table, bool[][] owned, int row, int column) =>
        !owned[row][column] && table.Rows[row].Cells[column].Covered;
}
