using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PulseOffice.Drawing.Scenes;

namespace PulseOffice.UI.Controls.Shapes.Painting;

public sealed class ScenePainter
{
    private const string Caption = "3D";

    private readonly IBrush _fill = new ImmutableLinearGradientBrush(
        [new ImmutableGradientStop(0, Color.FromRgb(0xF1, 0xF5, 0xF9)), new ImmutableGradientStop(1, Color.FromRgb(0xE2, 0xE8, 0xF0))],
        startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
        endPoint: new RelativePoint(1, 1, RelativeUnit.Relative));

    private readonly IPen _border = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)), 1, new ImmutableDashStyle([4, 3], 0));
    private readonly IPen _edges = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)), 1.5, lineJoin: PenLineJoin.Round);
    private readonly IBrush _top = new ImmutableSolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
    private readonly IBrush _side = new ImmutableSolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
    private readonly IBrush _front = new ImmutableSolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
    private readonly IBrush _ink = new ImmutableSolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));

    public void Paint(DrawingContext context, Scene3DObject scene, Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(scene);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var radius = Math.Min(8, Math.Min(bounds.Width, bounds.Height) / 8);
        context.DrawRectangle(_fill, _border, bounds, radius, radius);

        var size = Math.Min(bounds.Width, bounds.Height) * 0.36;
        if (size < 6) return;
        var depth = size * 0.35;
        var left = bounds.Center.X - (size + depth) / 2;
        var top = bounds.Center.Y - (size + depth) / 2 - size * 0.12;
        var front = new Rect(left, top + depth, size, size);
        Face(context, _top, new Point(front.Left, front.Top), new Point(front.Left + depth, top), new Point(front.Right + depth, top), new Point(front.Right, front.Top));
        Face(context, _side, new Point(front.Right, front.Top), new Point(front.Right + depth, top), new Point(front.Right + depth, top + size), new Point(front.Right, front.Bottom));
        context.DrawRectangle(_front, _edges, front);

        var text = new FormattedText(Caption, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold), Math.Max(8, size * 0.32), _ink);
        var below = front.Bottom + size * 0.12;
        if (below + text.Height <= bounds.Bottom) context.DrawText(text, new Point(bounds.Center.X - text.Width / 2, below));
    }

    private void Face(DrawingContext context, IBrush brush, params Point[] corners)
    {
        var geometry = new StreamGeometry();
        using (var sink = geometry.Open())
        {
            sink.BeginFigure(corners[0], true);
            foreach (var corner in corners.Skip(1)) sink.LineTo(corner);
            sink.EndFigure(true);
        }
        context.DrawGeometry(brush, _edges, geometry);
    }
}
