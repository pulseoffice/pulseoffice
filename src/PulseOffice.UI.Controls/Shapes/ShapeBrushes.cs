using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PulseOffice.Drawing.Shapes;

namespace PulseOffice.UI.Controls.Shapes;

public interface IShapeBrushes
{
    IBrush? Fill(ShapeFill fill, Rect bounds);

    Pen? Stroke(ShapeLine line);

    Color Colour(string? hex, Color fallback);

    Color? Average(ShapeBitmap bitmap);
}

public sealed class ShapeBrushes(PulseOffice.UI.Controls.Objects.IBitmapPixels pixels) : IShapeBrushes
{
    public ShapeBrushes() : this(new PulseOffice.UI.Controls.Objects.BitmapPixels()) { }

    public Color Colour(string? hex, Color fallback) =>
        hex is not null && Color.TryParse(hex, out var colour) ? colour : fallback;

    public IBrush? Fill(ShapeFill fill, Rect bounds)
    {
        ArgumentNullException.ThrowIfNull(fill);

        if (!fill.IsVisible) return null;

        return fill.Kind switch
        {
            ShapeFillKind.Gradient => Gradient(fill.Gradient, fill.Opacity),
            ShapeFillKind.Hatch => Hatch(fill.Hatch, fill.Opacity),
            ShapeFillKind.Bitmap when fill.Bitmap is { } bitmap => Picture(bitmap, fill.Opacity, bounds),
            _ => new SolidColorBrush(Colour(fill.ColorHex, Colors.SteelBlue), fill.Opacity),
        };
    }

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<byte[], Avalonia.Media.Imaging.Bitmap?> _pictures = [];

    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<byte[], StrongBox<Color?>> _averages = [];

    public Color? Average(ShapeBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return _averages.GetValue(bitmap.Data, data => new StrongBox<Color?>(Averaged(data))).Value;
    }

    private Color? Averaged(byte[] data)
    {
        if (_pictures.GetValue(data, pixels.Decode) is not { } source) return null;
        Color? found = null;
        pixels.Edit(source, image =>
        {
            double blue = 0, green = 0, red = 0, weight = 0;
            for (var index = 0; index + 3 < image.Pixels.Length; index += 4)
            {
                blue += image.Pixels[index];
                green += image.Pixels[index + 1];
                red += image.Pixels[index + 2];
                weight += image.Pixels[index + 3] / 255.0;
            }
            if (weight > 0) found = Color.FromRgb((byte)(red / weight), (byte)(green / weight), (byte)(blue / weight));
        }).Dispose();
        return found;
    }

    private readonly PulseOffice.Drawing.Assets.ImageHeader _header = new();


    private IBrush? Picture(ShapeBitmap bitmap, double opacity, Rect bounds)
    {
        var source = _pictures.GetValue(bitmap.Data, pixels.Decode);
        if (source is null) return null;
        if (!bitmap.Tiled) return new ImageBrush(source) { Stretch = Stretch.Fill, Opacity = opacity };

        var natural = _header.NaturalTwips(bitmap.Data);
        var width = bitmap.TileWidthRatio > 0 ? bitmap.TileWidthRatio * bounds.Width
            : bitmap.TileWidth.Value > 0 ? bitmap.TileWidth.ToPoints() : natural is { } size ? size.Width / ((double)PulseOffice.Foundation.Units.Twip.PerPoint) : source.Size.Width;
        var height = bitmap.TileHeightRatio > 0 ? bitmap.TileHeightRatio * bounds.Height
            : bitmap.TileHeight.Value > 0 ? bitmap.TileHeight.ToPoints() : natural is { } tall ? tall.Height / ((double)PulseOffice.Foundation.Units.Twip.PerPoint) : source.Size.Height;
        return new ImageBrush(source)
        {
            Stretch = Stretch.Fill,
            TileMode = TileMode.Tile,
            DestinationRect = new RelativeRect(0, 0, Math.Max(1, width), Math.Max(1, height), RelativeUnit.Absolute),
            Opacity = opacity,
        };
    }

    private IBrush Gradient(ShapeGradient gradient, double opacity)
    {
        var stops = new GradientStops();

        foreach (var stop in Spread(gradient))
            stops.Add(new Avalonia.Media.GradientStop(
                Colour(stop.ColorHex, Colors.SteelBlue), Math.Clamp(stop.Position, 0, 1)));

        if (gradient.Kind is GradientKind.Radial or GradientKind.Elliptical)
            return new RadialGradientBrush
            {
                GradientStops = stops,
                Opacity = opacity,
                Center = new RelativePoint(gradient.CentreX, gradient.CentreY, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(gradient.CentreX, gradient.CentreY, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.75, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(
                    gradient.Kind is GradientKind.Elliptical ? 0.5 : 0.75, RelativeUnit.Relative),
            };

        var radians = gradient.AngleDegrees * Math.PI / 180;

        var dx = Math.Cos(radians) / 2;
        var dy = Math.Sin(radians) / 2;

        return new LinearGradientBrush
        {
            GradientStops = stops,
            Opacity = opacity,
            StartPoint = new RelativePoint(0.5 - dx, 0.5 - dy, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0.5 + dx, 0.5 + dy, RelativeUnit.Relative),
        };
    }

    private IEnumerable<PulseOffice.Drawing.Shapes.GradientStop> Spread(ShapeGradient gradient)
    {
        var border = Math.Clamp(gradient.Border, 0, 1);
        var bordered = Ordered(gradient).Select(stop => stop with { Position = border + stop.Position * (1 - border) }).ToList();
        if (gradient.Kind is not GradientKind.Axial) return bordered;

        return bordered.Select(stop => stop with { Position = stop.Position / 2 })
            .Concat(bordered.AsEnumerable().Reverse().Select(stop => stop with { Position = 1 - stop.Position / 2 }));
    }

    private static IReadOnlyList<PulseOffice.Drawing.Shapes.GradientStop> Ordered(ShapeGradient gradient)
    {
        if (gradient.Stops.Count >= 2) return [.. gradient.Stops.OrderBy(stop => stop.Position)];

        return
        [
            new PulseOffice.Drawing.Shapes.GradientStop(gradient.StartHex, 0),
            new PulseOffice.Drawing.Shapes.GradientStop(gradient.EndHex, 1),
        ];
    }

    private IBrush Hatch(ShapeHatch hatch, double opacity)
    {
        var distance = Math.Max(2, hatch.Distance.ToPoints());
        var colour = new ImmutableSolidColorBrush(Colour(hatch.ColorHex, Colors.SteelBlue));

        var lines = new GeometryGroup();

        void Line(double angle)
        {
            var radians = angle * Math.PI / 180;

            var dx = Math.Cos(radians) * distance;
            var dy = Math.Sin(radians) * distance;

            lines.Children.Add(new LineGeometry(
                new Point(distance / 2 - dx, distance / 2 - dy),
                new Point(distance / 2 + dx, distance / 2 + dy)));
        }

        Line(hatch.AngleDegrees);
        if (hatch.Kind is not HatchKind.Single) Line(hatch.AngleDegrees + 90);
        if (hatch.Kind is HatchKind.Triple) Line(hatch.AngleDegrees + 45);

        var drawing = new DrawingGroup();

        if (hatch.BackgroundHex is { } background)
            drawing.Children.Add(new GeometryDrawing
            {
                Geometry = new RectangleGeometry(new Rect(0, 0, distance, distance)),
                Brush = new ImmutableSolidColorBrush(Colour(background, Colors.White)),
            });

        drawing.Children.Add(new GeometryDrawing
        {
            Geometry = lines,
            Pen = new Pen(colour, 1),
        });

        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            SourceRect = new RelativeRect(0, 0, distance, distance, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(0, 0, distance, distance, RelativeUnit.Absolute),
            Opacity = opacity,
        };
    }

    public Pen? Stroke(ShapeLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (!line.IsVisible) return null;

        return new Pen(
            new SolidColorBrush(Colour(line.ColorHex, Colors.Black), line.Opacity),
            Math.Max(0.5, line.Width.ToPoints()))
        {
            DashStyle = line.CustomDash is { } custom && line.Dash is not ShapeStrokeStyle.Solid and not ShapeStrokeStyle.None
                ? new DashStyle(custom.InLineWidths(Math.Max(1, line.Width.Value)), 0)
                : Dashes(line.Dash),
            LineCap = line.Cap switch
            {
                LineCapStyle.Round => PenLineCap.Round,
                LineCapStyle.Square => PenLineCap.Square,
                _ => PenLineCap.Flat,
            },
            LineJoin = line.Join switch
            {
                LineJoinStyle.Miter => PenLineJoin.Miter,
                LineJoinStyle.Bevel => PenLineJoin.Bevel,
                _ => PenLineJoin.Round,
            },
        };
    }

    private static DashStyle? Dashes(ShapeStrokeStyle dash) => dash switch
    {
        ShapeStrokeStyle.Dashed => new DashStyle([2, 2], 0),
        ShapeStrokeStyle.Dotted => new DashStyle([0.5, 2], 0),
        ShapeStrokeStyle.DashDot => new DashStyle([4, 2, 1, 2], 0),
        ShapeStrokeStyle.DashDotDot => new DashStyle([4, 2, 1, 2, 1, 2], 0),
        ShapeStrokeStyle.LongDash => new DashStyle([8, 3], 0),
        _ => null,
    };
}
