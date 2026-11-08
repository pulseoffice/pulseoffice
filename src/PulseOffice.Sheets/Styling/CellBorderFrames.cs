using PulseOffice.Document.Borders;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Sheets.Styling;

public interface ICellBorderFrames
{
    BorderFrameScope Scope(bool rows, bool columns);

    BorderStroke? Stroke(BorderEdge edge);

    BorderEdge Edge(BorderStroke? stroke);

    BorderFrame Frame(CellBorders borders, bool rows, bool columns);

    CellBorders Apply(CellBorders borders, CellBorderEdits edits);
}

public sealed class CellBorderFrames : ICellBorderFrames
{
    private const double ThinLimit = 1;
    private const double MediumLimit = 2;

    public BorderFrameScope Scope(bool rows, bool columns) => new(rows, columns, true);

    public BorderStroke? Stroke(BorderEdge edge)
    {
        ArgumentNullException.ThrowIfNull(edge);
        if (!edge.IsVisible) return null;
        var dash = edge.Style switch
        {
            BorderStyle.Dashed => BorderDash.Dashed,
            BorderStyle.Dotted => BorderDash.Dotted,
            BorderStyle.Double => BorderDash.Double,
            _ => BorderDash.Solid,
        };
        return new BorderStroke(dash, Math.Round(edge.Width.ToPoints(), 2), edge.ColorHex);
    }

    public BorderEdge Edge(BorderStroke? stroke)
    {
        if (stroke is null || stroke.WidthPoints <= 0) return BorderEdge.None;
        var style = stroke.Dash switch
        {
            BorderDash.Dashed => BorderStyle.Dashed,
            BorderDash.Dotted => BorderStyle.Dotted,
            BorderDash.Double => BorderStyle.Double,
            _ when stroke.WidthPoints <= ThinLimit => BorderStyle.Thin,
            _ when stroke.WidthPoints <= MediumLimit => BorderStyle.Medium,
            _ => BorderStyle.Thick,
        };
        return new BorderEdge(style, stroke.ColorHex, Twip.FromPoints(stroke.WidthPoints));
    }

    public BorderFrame Frame(CellBorders borders, bool rows, bool columns)
    {
        ArgumentNullException.ThrowIfNull(borders);
        var frame = BorderFrame.Empty
            .With(BorderPosition.Top, Stroke(borders.Top))
            .With(BorderPosition.Bottom, Stroke(borders.Bottom))
            .With(BorderPosition.Left, Stroke(borders.Left))
            .With(BorderPosition.Right, Stroke(borders.Right))
            .With(BorderPosition.DiagonalDown, Stroke(borders.DiagonalDown))
            .With(BorderPosition.DiagonalUp, Stroke(borders.DiagonalUp));
        if (rows) frame = frame.With(BorderPosition.InsideHorizontal, Stroke(borders.Bottom));
        if (columns) frame = frame.With(BorderPosition.InsideVertical, Stroke(borders.Right));
        return frame;
    }

    public CellBorders Apply(CellBorders borders, CellBorderEdits edits)
    {
        ArgumentNullException.ThrowIfNull(borders);
        ArgumentNullException.ThrowIfNull(edits);
        if (edits.IsEmpty) return borders;
        return new CellBorders(
            edits.Left.Apply(borders.Left, Edge),
            edits.Top.Apply(borders.Top, Edge),
            edits.Right.Apply(borders.Right, Edge),
            edits.Bottom.Apply(borders.Bottom, Edge))
        {
            DiagonalDown = edits.DiagonalDown.Apply(borders.DiagonalDown, Edge),
            DiagonalUp = edits.DiagonalUp.Apply(borders.DiagonalUp, Edge),
        };
    }
}
