using Avalonia;
using Avalonia.Media;
using PulseOffice.Drawing.ConditionalIcons;

namespace PulseOffice.UI.Controls.Shapes;

public sealed class IconPainter(OutlineGeometry geometry)
{
    public IconPainter() : this(new OutlineGeometry()) { }

    public void Paint(DrawingContext context, ConditionalIcon icon, Rect box)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(icon);

        foreach (var layer in icon.Layers)
        {
            var path = geometry.Build(layer.Outline, point => new Point(box.X + point.X * box.Width, box.Y + point.Y * box.Height), layer.Fill is not null);
            var fill = layer.Fill is { } colour ? new SolidColorBrush(Color.Parse(colour)) : null;
            var pen = layer.Stroke is { } line && layer.StrokeWidth > 0
                ? new Pen(new SolidColorBrush(Color.Parse(line)), Math.Max(1, layer.StrokeWidth * box.Width), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round)
                : null;
            context.DrawGeometry(fill, pen, path);
        }
    }
}
