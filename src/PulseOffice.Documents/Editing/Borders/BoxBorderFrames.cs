using PulseOffice.Foundation.Colors;
using System.Globalization;
using PulseOffice.Document.Borders;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Editing.Borders;

public interface IBoxBorderFrames
{
    BorderStroke? Stroke(BorderLine? line);

    BorderLine? Line(BorderStroke? stroke);

    BorderFrame Frame(BoxBorders? borders);

    BoxBorders Apply(BoxBorders? borders, BorderFrame frame, double padding);

    BoxBorders Apply(BoxBorders? borders, CellBorderEdits edits);

    BorderFrame Frame(BoxBorders? topLeft, BoxBorders? bottomRight, bool rows, bool columns);
}

public sealed class BoxBorderFrames : IBoxBorderFrames
{
    public BorderStroke? Stroke(BorderLine? line) => line is null ? null : new BorderStroke(Dash(line.Style), line.Width, Hex(line.Color));

    public BorderLine? Line(BorderStroke? stroke) => stroke is null ? null : new BorderLine(stroke.WidthPoints, Argb(stroke.ColorHex)) { Style = Style(stroke.Dash) };

    public BorderFrame Frame(BoxBorders? borders) => borders is null ? BorderFrame.Empty : BorderFrame.Empty
        .With(BorderPosition.Top, Stroke(borders.Top))
        .With(BorderPosition.Bottom, Stroke(borders.Bottom))
        .With(BorderPosition.Left, Stroke(borders.Left))
        .With(BorderPosition.Right, Stroke(borders.Right));

    public BorderFrame Frame(BoxBorders? topLeft, BoxBorders? bottomRight, bool rows, bool columns)
    {
        var frame = BorderFrame.Empty
            .With(BorderPosition.Top, Stroke(topLeft?.Top))
            .With(BorderPosition.Left, Stroke(topLeft?.Left))
            .With(BorderPosition.Bottom, Stroke(bottomRight?.Bottom))
            .With(BorderPosition.Right, Stroke(bottomRight?.Right));
        if (rows) frame = frame.With(BorderPosition.InsideHorizontal, Stroke(topLeft?.Bottom));
        if (columns) frame = frame.With(BorderPosition.InsideVertical, Stroke(topLeft?.Right));
        return frame;
    }

    public BoxBorders Apply(BoxBorders? borders, BorderFrame frame, double padding)
    {
        ArgumentNullException.ThrowIfNull(frame);
        return (borders ?? new BoxBorders()) with
        {
            Top = Line(frame.Stroke(BorderPosition.Top)),
            Bottom = Line(frame.Stroke(BorderPosition.Bottom)),
            Left = Line(frame.Stroke(BorderPosition.Left)),
            Right = Line(frame.Stroke(BorderPosition.Right)),
            PaddingTop = frame.Has(BorderPosition.Top) ? padding : 0,
            PaddingBottom = frame.Has(BorderPosition.Bottom) ? padding : 0,
            PaddingLeft = frame.Has(BorderPosition.Left) ? padding : 0,
            PaddingRight = frame.Has(BorderPosition.Right) ? padding : 0,
        };
    }

    public BoxBorders Apply(BoxBorders? borders, CellBorderEdits edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var current = borders ?? new BoxBorders();
        return current with
        {
            Top = edits.Top.Apply(current.Top, Line),
            Bottom = edits.Bottom.Apply(current.Bottom, Line),
            Left = edits.Left.Apply(current.Left, Line),
            Right = edits.Right.Apply(current.Right, Line),
        };
    }

    private BorderDash Dash(BorderLineStyle style) => style switch
    {
        BorderLineStyle.Dotted => BorderDash.Dotted,
        BorderLineStyle.Dashed => BorderDash.Dashed,
        BorderLineStyle.Double => BorderDash.Double,
        _ => BorderDash.Solid,
    };

    private BorderLineStyle Style(BorderDash dash) => dash switch
    {
        BorderDash.Dotted => BorderLineStyle.Dotted,
        BorderDash.Dashed => BorderLineStyle.Dashed,
        BorderDash.Double => BorderLineStyle.Double,
        _ => BorderLineStyle.Solid,
    };

    private string Hex(uint argb) => new ArgbColor(argb).ToHashedHex();

    private uint Argb(string hex) =>
        uint.TryParse(hex.TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb) ? 0xFF000000 | (rgb & 0xFFFFFF) : 0xFF000000;
}
