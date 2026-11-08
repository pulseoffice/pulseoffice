using DocumentFormat.OpenXml;
using PulseOffice.Text.Rich;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxBorders(DocxValues values)
{
    private const uint LargestSize = 96;
    private const uint LargestSpace = 31;

    private readonly IReadOnlyList<(BorderLineStyle Style, W.BorderValues Value)> _styles =
    [
        (BorderLineStyle.Solid, W.BorderValues.Single),
        (BorderLineStyle.Dotted, W.BorderValues.Dotted),
        (BorderLineStyle.Dashed, W.BorderValues.Dashed),
        (BorderLineStyle.Double, W.BorderValues.Double),
    ];

    public T Write<T>(T container, BoxBorders borders) where T : OpenXmlCompositeElement
    {
        container.Append(
            Side(new W.TopBorder(), borders.Top, borders.PaddingTop),
            Side(new W.LeftBorder(), borders.Left, borders.PaddingLeft),
            Side(new W.BottomBorder(), borders.Bottom, borders.PaddingBottom),
            Side(new W.RightBorder(), borders.Right, borders.PaddingRight));
        return container;
    }

    public BoxBorders Read(OpenXmlElement container)
    {
        var (top, topSpace) = Side(container.GetFirstChild<W.TopBorder>());
        var (left, leftSpace) = Side((W.BorderType?)container.GetFirstChild<W.LeftBorder>() ?? container.GetFirstChild<W.StartBorder>());
        var (bottom, bottomSpace) = Side(container.GetFirstChild<W.BottomBorder>());
        var (right, rightSpace) = Side((W.BorderType?)container.GetFirstChild<W.RightBorder>() ?? container.GetFirstChild<W.EndBorder>());
        return new BoxBorders
        {
            Top = top,
            Left = left,
            Bottom = bottom,
            Right = right,
            PaddingTop = topSpace,
            PaddingLeft = leftSpace,
            PaddingBottom = bottomSpace,
            PaddingRight = rightSpace,
        };
    }

    private int Lines(BorderLineStyle style) => style == BorderLineStyle.Double ? 3 : 1;

    private W.BorderType Side(W.BorderType border, BorderLine? line, double padding)
    {
        if (line is null || line.Width <= 0)
        {
            border.Val = W.BorderValues.Nil;
            if (padding > 0) border.Space = (uint)Math.Clamp(Math.Round(padding), 0, 31);
            return border;
        }
        border.Val = _styles.FirstOrDefault(entry => entry.Style == line.Style, _styles[0]).Value;
        border.Size = (uint)Math.Clamp(Math.Round(line.Width * 8 / Lines(line.Style)), 2, 96);
        border.Space = (uint)Math.Clamp(Math.Round(padding), 0, 31);
        border.Color = values.Color(line.Color);
        return border;
    }

    private (BorderLine? Line, double Space) Side(W.BorderType? border)
    {
        if (border is null) return (null, 0);
        var space = Math.Min(border.Space?.Value ?? 0, LargestSpace);
        var value = border.Val?.Value;
        if (value is null || value == W.BorderValues.None || value == W.BorderValues.Nil) return (null, space);
        var style = _styles.FirstOrDefault(entry => entry.Value == value.Value, _styles[0]).Style;
        if (value == W.BorderValues.DotDash || value == W.BorderValues.DashSmallGap || value == W.BorderValues.DotDotDash) style = BorderLineStyle.Dashed;
        if (value == W.BorderValues.Triple || value == W.BorderValues.ThinThickSmallGap || value == W.BorderValues.ThickThinSmallGap) style = BorderLineStyle.Double;
        var width = Math.Min(border.Size?.Value ?? 4, LargestSize) / 8.0 * Lines(style);
        return (new BorderLine(width, values.Color(border.Color?.Value) ?? 0xFF000000) { Style = style }, space);
    }
}
