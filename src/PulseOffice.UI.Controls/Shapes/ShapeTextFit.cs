using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.UI.Controls.Shapes;

public interface IShapeTextFit
{
    bool Resizes(ShapeObject shape);

    Twip? Height(ShapeObject shape, DocumentSize size, TextFields? fields = null);

    DocumentSize? Size(ShapeObject shape, DocumentSize size, TextFields? fields = null);
}

public sealed class ShapeTextFit(ITextFrameMeasure measure, IShapeListText listText, IShapeTextArea area) : IShapeTextFit
{

    public ShapeTextFit() : this(new TextFrameMeasure(new ListLabels()), new ShapeListText(), new ShapeTextArea())
    {
    }

    public bool Resizes(ShapeObject shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        return shape.Style.Text.Autofit == TextAutofit.ResizeShape
            && shape.Dimension is null
            && shape.Shape != ShapeKind.Line
            && (!string.IsNullOrEmpty(shape.Text) || shape.Runs is { Count: > 0 });
    }

    public Twip? Height(ShapeObject shape, DocumentSize size, TextFields? fields = null)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (!Resizes(shape)) return null;
        var framed = listText.Framed(shape, 0);
        return framed.Frame.Layout.Flow == TextFlow.Horizontal ? Extent(shape, framed, size.Width.ToPoints(), size.Height.ToPoints(), true, fields) : null;
    }

    public DocumentSize? Size(ShapeObject shape, DocumentSize size, TextFields? fields = null)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (!Resizes(shape)) return null;
        var framed = listText.Framed(shape, 0);
        var width = size.Width.ToPoints();
        var height = size.Height.ToPoints();
        return framed.Frame.Layout.Flow switch
        {
            TextFlow.Horizontal => new DocumentSize(size.Width, Extent(shape, framed, width, height, true, fields)),
            TextFlow.Vertical or TextFlow.Vertical270 or TextFlow.EastAsianVertical or TextFlow.MongolianVertical
                => new DocumentSize(Extent(shape, framed with { Frame = framed.Frame with { Layout = framed.Frame.Layout with { Flow = TextFlow.Horizontal } } }, width, height, false, fields), size.Height),
            _ => null,
        };
    }

    private Twip Extent(ShapeObject shape, RichText framed, double width, double height, bool horizontal, TextFields? fields)
    {
        var margins = area.Margins(shape, width, height);
        var (line, across, inner) = horizontal
            ? (margins.Width(width), height, margins.Height(height))
            : (margins.Height(height), width, margins.Width(width));
        var share = across > 0 ? Math.Clamp(inner / across, 0.05, 1) : 1;
        var content = measure.Height(framed, line * Twip.PerPoint, listText.Theme(shape, default), fields ?? new TextFields(0, DateTime.Now)) / Twip.PerPoint;
        var fitted = (int)Math.Ceiling(content / share * Twip.PerPoint);
        return new Twip(Math.Max(1, fitted));
    }
}
