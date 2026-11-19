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
                var right = column + Math.Max(1, cell.ColumnSpan);
                if (right < looks[row].Length && looks[row][right] is not null && !table.Cell(row, right).Covered)
                {
                    var winner = Winner(looks[row][column]!.Borders.Right, looks[row][right]!.Borders.Left);
                    looks[row][column] = looks[row][column]! with { Borders = looks[row][column]!.Borders with { Right = winner } };
                    looks[row][right] = looks[row][right]! with { Borders = looks[row][right]!.Borders with { Left = winner } };
                }
                var below = row + Math.Max(1, cell.RowSpan);
                if (below < looks.Length && column < looks[below].Length && looks[below][column] is not null && !table.Cell(below, column).Covered)
                {
                    var winner = Winner(looks[row][column]!.Borders.Bottom, looks[below][column]!.Borders.Top);
                    looks[row][column] = looks[row][column]! with { Borders = looks[row][column]!.Borders with { Bottom = winner } };
                    looks[below][column] = looks[below][column]! with { Borders = looks[below][column]!.Borders with { Top = winner } };
                }
            }
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
