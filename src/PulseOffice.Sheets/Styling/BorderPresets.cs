using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Styling;

public enum BorderPreset
{
    None,
    All,
    Outside,
    ThickOutside,
    Inside,
    InsideHorizontal,
    InsideVertical,
    Left,
    Right,
    Top,
    Bottom,
    TopBottom,
    DoubleBottom,
    ThickBottom,
    DiagonalUp,
    DiagonalDown,
}

public interface IBorderPresets
{
    IReadOnlyList<BorderPreset> All { get; }

    IReadOnlyList<BorderStyle> LineStyles { get; }

    BorderEdge Pen(BorderStyle style, string colorHex);

    CellBorders Apply(CellBorders current, BorderPreset preset, BorderEdge pen, CellRange range, CellAddress cell);
}

public sealed class BorderPresets : IBorderPresets
{
    public IReadOnlyList<BorderPreset> All { get; } = Enum.GetValues<BorderPreset>();

    public IReadOnlyList<BorderStyle> LineStyles { get; } =
        [BorderStyle.Thin, BorderStyle.Medium, BorderStyle.Thick, BorderStyle.Dashed, BorderStyle.Dotted, BorderStyle.Double];

    public BorderEdge Pen(BorderStyle style, string colorHex) =>
        style == BorderStyle.None
            ? BorderEdge.None
            : new BorderEdge(style, colorHex, Twip.FromPoints(style switch
            {
                BorderStyle.Medium => 1.5,
                BorderStyle.Thick => 2.5,
                BorderStyle.Double => 2.25,
                _ => 0.75,
            }));

    public CellBorders Apply(CellBorders current, BorderPreset preset, BorderEdge pen, CellRange range, CellAddress cell)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(pen);

        var first = cell.Row == range.Start.Row;
        var last = cell.Row == range.End.Row;
        var leftmost = cell.Column == range.Start.Column;
        var rightmost = cell.Column == range.End.Column;

        var thick = Pen(BorderStyle.Thick, pen.ColorHex);

        return preset switch
        {
            BorderPreset.None => CellBorders.None,
            BorderPreset.All => new CellBorders(pen, pen, pen, pen)
            {
                DiagonalUp = current.DiagonalUp,
                DiagonalDown = current.DiagonalDown,
            },
            BorderPreset.Outside => Outline(current, pen, first, last, leftmost, rightmost),
            BorderPreset.ThickOutside => Outline(current, thick, first, last, leftmost, rightmost),
            BorderPreset.Inside => current with
            {
                Left = leftmost ? current.Left : pen,
                Right = rightmost ? current.Right : pen,
                Top = first ? current.Top : pen,
                Bottom = last ? current.Bottom : pen,
            },
            BorderPreset.InsideHorizontal => current with
            {
                Top = first ? current.Top : pen,
                Bottom = last ? current.Bottom : pen,
            },
            BorderPreset.InsideVertical => current with
            {
                Left = leftmost ? current.Left : pen,
                Right = rightmost ? current.Right : pen,
            },
            BorderPreset.Left => leftmost ? current with { Left = pen } : current,
            BorderPreset.Right => rightmost ? current with { Right = pen } : current,
            BorderPreset.Top => first ? current with { Top = pen } : current,
            BorderPreset.Bottom => last ? current with { Bottom = pen } : current,
            BorderPreset.TopBottom => current with
            {
                Top = first ? pen : current.Top,
                Bottom = last ? pen : current.Bottom,
            },
            BorderPreset.DoubleBottom => last ? current with { Bottom = Pen(BorderStyle.Double, pen.ColorHex) } : current,
            BorderPreset.ThickBottom => last ? current with { Bottom = thick } : current,
            BorderPreset.DiagonalUp => current with { DiagonalUp = pen },
            BorderPreset.DiagonalDown => current with { DiagonalDown = pen },
            _ => current,
        };
    }

    private CellBorders Outline(
        CellBorders current, BorderEdge pen, bool first, bool last, bool leftmost, bool rightmost) =>
        current with
        {
            Left = leftmost ? pen : current.Left,
            Right = rightmost ? pen : current.Right,
            Top = first ? pen : current.Top,
            Bottom = last ? pen : current.Bottom,
        };
}
