using PulseOffice.Documents.Blocks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Styles;

public interface ICellBorderConflicts
{
    BorderLine? Winner(BorderLine? leading, BorderLine? trailing);

    void Resolve(TableBlock table, CellAppearance?[][] looks);
}

public sealed class CellBorderConflicts : ICellBorderConflicts
{
    public BorderLine? Winner(BorderLine? leading, BorderLine? trailing)
    {
        if (leading is null) return trailing;
        if (trailing is null) return leading;
        var weight = Weight(leading).CompareTo(Weight(trailing));
        if (weight != 0) return weight > 0 ? leading : trailing;
        var style = Rank(leading.Style).CompareTo(Rank(trailing.Style));
        if (style != 0) return style < 0 ? leading : trailing;
        var darkness = Brightness(trailing.Color).CompareTo(Brightness(leading.Color));
        if (darkness != 0) return darkness > 0 ? leading : trailing;
        return leading;
    }

    public void Resolve(TableBlock table, CellAppearance?[][] looks)
    {
        for (var row = 0; row < looks.Length; row++)
            for (var column = 0; column < looks[row].Length; column++)
            {
                if (looks[row][column] is null || table.Cell(row, column).Covered) continue;
                var cell = table.Cell(row, column);
                ResolveRight(table, looks, row, column, cell);
                ResolveBelow(table, looks, row, column, cell);
            }
    }

    private void ResolveRight(TableBlock table, CellAppearance?[][] looks, int row, int column, TableCell cell)
    {
        var right = column + Math.Max(1, cell.ColumnSpan);
        var own = looks[row][column]!.Borders.Right;
        var winners = new List<BorderLine?>();
        var complete = true;
        for (var r = row; r < Math.Min(looks.Length, row + Math.Max(1, cell.RowSpan)); r++)
        {
            if (right >= looks[r].Length || looks[r][right] is not { } neighbour || table.Cell(r, right).Covered)
            {
                complete = false;
                continue;
            }
            var winner = Winner(own, neighbour.Borders.Left);
            looks[r][right] = neighbour with { Borders = neighbour.Borders with { Left = winner } };
            winners.Add(winner);
        }
        if (winners.Count == 0) return;
        var shared = winners.All(winner => Equals(winner, winners[0])) ? winners[0] : complete ? null : own ?? winners[0];
        looks[row][column] = looks[row][column]! with { Borders = looks[row][column]!.Borders with { Right = shared } };
    }

    private void ResolveBelow(TableBlock table, CellAppearance?[][] looks, int row, int column, TableCell cell)
    {
        var below = row + Math.Max(1, cell.RowSpan);
        if (below >= looks.Length) return;
        var own = looks[row][column]!.Borders.Bottom;
        var winners = new List<BorderLine?>();
        var complete = true;
        for (var c = column; c < Math.Min(looks[below].Length, column + Math.Max(1, cell.ColumnSpan)); c++)
        {
            if (looks[below][c] is not { } neighbour || table.Cell(below, c).Covered)
            {
                complete = false;
                continue;
            }
            var winner = Winner(own, neighbour.Borders.Top);
            looks[below][c] = neighbour with { Borders = neighbour.Borders with { Top = winner } };
            winners.Add(winner);
        }
        if (winners.Count == 0) return;
        var shared = winners.All(winner => Equals(winner, winners[0])) ? winners[0] : complete ? null : own ?? winners[0];
        looks[row][column] = looks[row][column]! with { Borders = looks[row][column]!.Borders with { Bottom = shared } };
    }

    private double Weight(BorderLine line) => Math.Max(0, line.Width) * Rank(line.Style);

    private int Rank(BorderLineStyle style) => style switch
    {
        BorderLineStyle.Double => 3,
        BorderLineStyle.Dotted => 4,
        BorderLineStyle.Dashed => 5,
        _ => 1,
    };

    private int Brightness(uint argb)
    {
        var red = (int)((argb >> 16) & 0xFF);
        var green = (int)((argb >> 8) & 0xFF);
        var blue = (int)(argb & 0xFF);
        return red + blue + 2 * green;
    }
}
