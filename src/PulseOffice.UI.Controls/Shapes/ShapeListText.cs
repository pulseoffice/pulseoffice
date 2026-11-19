using Avalonia;
using Avalonia.Media;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.UI.Controls.Shapes;

public interface IShapeListText
{
    bool Draws(ShapeObject shape);

    void Draw(DrawingContext context, ShapeObject shape, Rect bounds, Color ink, string? language = null, double shapeRotation = 0, IPaintedTextLines? painted = null);

    RichText Framed(ShapeObject shape, double shapeRotation);

    ITextTheme Theme(ShapeObject shape, Color ink);
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

    private readonly TextFlowFrame _flow = new();

    public bool Draws(ShapeObject shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return lists.Listed(shape) || lists.Directed(shape) || Composed(shape.Style.Text.Layout);
    }

    public void Draw(DrawingContext context, ShapeObject shape, Rect bounds, Color ink, string? language = null, double shapeRotation = 0, IPaintedTextLines? painted = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(shape);
        painter.Paint(new RichTextScene(context, bounds, PointScale, Theme(shape, ink), new TextFields(0, DateTime.Now, Language: language)) { Painted = painted }, Framed(shape, shapeRotation));
    }

    public ITextTheme Theme(ShapeObject shape, Color ink)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return new ShapeTextTheme(ink.ToUInt32(), shape.Style.Text.Family);
    }

    public RichText Framed(ShapeObject shape, double shapeRotation)
    {
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
                Autofit = style.Autofit,
                Layout = style.Layout,
                RotationDegrees = style.Layout.Upright ? style.RotationDegrees : style.RotationDegrees + shapeRotation,
            },
        };
        return framed;
    }

    private bool Composed(TextLayout layout) =>
        _flow.Composes(layout);
}
