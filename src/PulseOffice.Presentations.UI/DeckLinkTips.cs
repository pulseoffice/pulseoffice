using Avalonia;
using PulseOffice.UI.Controls.Links;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.Presentations.UI;

public interface IDeckLinkTips
{
    string? At(SlideView canvas, Point point);

    void Show(Avalonia.Controls.Control target, string? text);
}

public sealed class DeckLinkTips(ILinkTips tips, IRichTextLinkHit hits, ITextFrameComposer composer, PulseOffice.UI.Controls.Shapes.IShapeListText shapeText) : IDeckLinkTips
{
    public DeckLinkTips(ILinkTips tips, IRichTextLinkHit hits, ITextFrameComposer composer)
        : this(tips, hits, composer, new PulseOffice.UI.Controls.Shapes.ShapeListText())
    {
    }

    public string? At(SlideView canvas, Point point)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (canvas.Slide is not { } slide || canvas.ElementAt(point) is not { } element) return null;
        if (element is TextElement text && hits.At(canvas.Compose(composer, text), canvas.Fields, Unrotated(canvas.BoundsOf(text), text.RotationDegrees, point)) is { } link)
            return tips.Text(link.Address, link.Tip);
        if (element is ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject shape } && ShapeLink(canvas, element, shape, point) is { } shapeLink)
            return tips.Text(shapeLink.Address, shapeLink.Tip);
        return slide.Interaction(element.Id) is { Kind: InteractionKind.Hyperlink } action ? tips.Text(action.Reference, action.Tip) : null;
    }

    private RichTextLink? ShapeLink(SlideView canvas, SlideElement element, PulseOffice.Drawing.Shapes.ShapeObject shape, Point point)
    {
        if (shape.Runs is not { Count: > 0 } runs || !runs.Any(run => run.Link is not null)) return null;
        var bounds = canvas.BoundsOf(element);
        var frame = canvas.Compose(composer, shapeText.Framed(shape, shape.RotationDegrees), bounds, shapeText.Theme(shape, default));
        return hits.At(frame, canvas.Fields, Unrotated(bounds, shape.RotationDegrees, point));
    }

    public void Show(Avalonia.Controls.Control target, string? text) => tips.Show(target, text);

    private Point Unrotated(Rect bounds, double degrees, Point point)
    {
        if (degrees == 0 || !double.IsFinite(degrees)) return point;
        var centre = bounds.Center;
        var radians = -degrees * Math.PI / 180;
        var (dx, dy) = (point.X - centre.X, point.Y - centre.Y);
        return new Point(centre.X + dx * Math.Cos(radians) - dy * Math.Sin(radians), centre.Y + dx * Math.Sin(radians) + dy * Math.Cos(radians));
    }
}
