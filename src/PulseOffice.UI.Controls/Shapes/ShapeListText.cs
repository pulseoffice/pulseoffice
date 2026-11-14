using Avalonia;
using Avalonia.Media;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.UI.Controls.Shapes;

public interface IShapeListText
{
    bool Draws(ShapeObject shape);

    void Draw(DrawingContext context, ShapeObject shape, Rect bounds, Color ink, string? language = null);
}

public sealed record ShapeTextTheme(uint Ink, string Family) : ITextTheme
{
    public uint Resolve(ColorRole role) => Ink;

    public string MajorFont => Family;

    public string MinorFont => Family;
}

public sealed class ShapeListText(IShapeTextLists lists, IShapeRichText rich, IRichTextFramePainter painter) : IShapeListText
{
    private const double PointScale = 1 / ParagraphTypesetter.PointToTwip;

    public ShapeListText() : this(new ShapeTextLists(), new ShapeRichText(), new RichTextFramePainter(new TextFrameComposer(new ListLabels())))
    {
    }

    public bool Draws(ShapeObject shape) => lists.Listed(shape) || lists.Directed(shape);

    public void Draw(DrawingContext context, ShapeObject shape, Rect bounds, Color ink, string? language = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(shape);
        var style = shape.Style.Text;
        var align = style.Horizontal switch
        {
            TextHorizontalPlacement.Centre => TextAlign.Center,
            TextHorizontalPlacement.Right => TextAlign.Right,
            TextHorizontalPlacement.Justify => TextAlign.Justify,
            _ => TextAlign.Left,
        };
        var inset = Math.Max(0, style.Inset.ToPoints());
        var text = rich.ToRich(shape);
        var framed = text with
        {
            Paragraphs = [.. text.Paragraphs.Select(paragraph => paragraph with { Align = align, Bold = style.Bold, Italic = style.Italic })],
            Frame = new TextFrame
            {
                Anchor = style.Vertical switch
                {
                    TextVerticalPlacement.Top => TextAnchor.Top,
                    TextVerticalPlacement.Bottom => TextAnchor.Bottom,
                    _ => TextAnchor.Middle,
                },
                Insets = new TextInsets(inset, inset, inset, inset),
                WordWrap = style.Wrap,
            },
        };
        painter.Paint(new RichTextScene(context, bounds, PointScale, new ShapeTextTheme(ink.ToUInt32(), style.Family), new TextFields(0, DateTime.Now, Language: language)), framed);
    }
}
