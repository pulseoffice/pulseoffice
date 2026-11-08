using PulseOffice.Foundation.Colors;
using System.Globalization;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Foundation.Units;

namespace PulseOffice.Presentations.Decorations;

public sealed class DecorationCanvas(string prefix)
{
    public const double Width = 33.867;

    public const double Height = 19.05;

    private readonly List<SlideElement> _elements = [];

    public IReadOnlyList<SlideElement> Elements => _elements;

    public DecorationCanvas Rect(double left, double top, double width, double height, uint argb, double opacity = 1) =>
        Add(ShapeKind.Rectangle, left, top, width, height, Solid(argb, opacity));

    public DecorationCanvas Ellipse(double left, double top, double width, double height, uint argb, double opacity = 1) =>
        Add(ShapeKind.Ellipse, left, top, width, height, Solid(argb, opacity));

    public DecorationCanvas Ring(double left, double top, double width, double height, uint argb, double points, double opacity = 1) =>
        Add(ShapeKind.Ellipse, left, top, width, height, Stroke(argb, points, opacity));

    public DecorationCanvas Frame(double left, double top, double width, double height, uint argb, double points, double opacity = 1) =>
        Add(ShapeKind.Rectangle, left, top, width, height, Stroke(argb, points, opacity));

    public DecorationCanvas Slant(double left, double top, double width, double height, uint argb, double opacity = 1, bool mirrored = false) =>
        Add(ShapeKind.Parallelogram, left, top, width, height, Solid(argb, opacity), flipHorizontal: mirrored);

    public DecorationCanvas Corner(Corner corner, double size, uint argb, double opacity = 1) =>
        Corner(corner, size, size, argb, opacity);

    public DecorationCanvas Corner(Corner corner, double width, double height, uint argb, double opacity = 1)
    {
        var right = corner is Decorations.Corner.TopRight or Decorations.Corner.BottomRight;
        var top = corner is Decorations.Corner.TopLeft or Decorations.Corner.TopRight;
        return Add(ShapeKind.RightTriangle, right ? Width - width : 0, top ? 0 : Height - height, width, height,
            Solid(argb, opacity), flipHorizontal: right, flipVertical: top);
    }

    public DecorationCanvas Gradient(double left, double top, double width, double height, uint from, uint to, double angle) =>
        Add(ShapeKind.Rectangle, left, top, width, height, ShapeStyle.Default with
        {
            Fill = new ShapeFill
            {
                Kind = ShapeFillKind.Gradient,
                ColorHex = Hex(from),
                Gradient = new ShapeGradient { Kind = GradientKind.Linear, AngleDegrees = angle, Stops = [new(Hex(from), 0), new(Hex(to), 1)] },
            },
            Line = ShapeLine.None,
        });

    public uint Blend(uint from, uint to, double amount)
    {
        var t = Math.Clamp(amount, 0, 1);
        uint Channel(int shift) => (uint)Math.Round(((from >> shift) & 0xFF) * (1 - t) + ((to >> shift) & 0xFF) * t);
        return 0xFF000000u | Channel(16) << 16 | Channel(8) << 8 | Channel(0);
    }

    private DecorationCanvas Add(
        ShapeKind kind, double left, double top, double width, double height, ShapeStyle style,
        bool flipHorizontal = false, bool flipVertical = false)
    {
        var id = $"{prefix}{_elements.Count + 1}";
        _elements.Add(new ObjectElement
        {
            Id = id,
            ZOrder = _elements.Count,
            Bounds = new DocumentRect(Twip.FromCentimeters(left), Twip.FromCentimeters(top),
                Twip.FromCentimeters(width), Twip.FromCentimeters(height)),
            Content = new ShapeObject
            {
                Id = id,
                Shape = kind,
                Style = style,
                FlipHorizontal = flipHorizontal,
                FlipVertical = flipVertical,
            },
        });
        return this;
    }

    private ShapeStyle Solid(uint argb, double opacity) => ShapeStyle.Default with
    {
        Fill = new ShapeFill { Kind = ShapeFillKind.Solid, ColorHex = Hex(argb), Opacity = opacity },
        Line = ShapeLine.None,
    };

    private ShapeStyle Stroke(uint argb, double points, double opacity) => ShapeStyle.Default with
    {
        Fill = ShapeFill.None,
        Line = new ShapeLine { ColorHex = Hex(argb), Width = Twip.FromPoints(points), Opacity = opacity },
    };

    private string Hex(uint argb) => new ArgbColor(argb).ToHashedHex();
}

public enum Corner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}
