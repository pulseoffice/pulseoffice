using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;
using PulseOffice.Text.Rich;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.UI.Controls.Shapes;

public interface IShapeTextFit
{
    bool Resizes(ShapeObject shape);

    Twip? Height(ShapeObject shape, DocumentSize size, TextFields? fields = null);
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
        if (framed.Frame.Layout.Flow != TextFlow.Horizontal) return null;
        var width = size.Width.ToPoints();
        var height = size.Height.ToPoints();
        var margins = area.Margins(shape, width, height);
        var share = height > 0 ? Math.Clamp(margins.Height(height) / height, 0.05, 1) : 1;
        var content = measure.Height(framed, margins.Width(width) * Twip.PerPoint, listText.Theme(shape, default), fields ?? new TextFields(0, DateTime.Now)) / Twip.PerPoint;
        var fitted = (int)Math.Ceiling(content / share * Twip.PerPoint);
        return new Twip(Math.Max(1, fitted));
    }
}
