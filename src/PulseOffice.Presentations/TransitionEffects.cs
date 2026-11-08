namespace PulseOffice.Presentations;

public abstract record TransitionShape
{
    public abstract double Area { get; }
}

public sealed record TransitionRectangle(double X, double Y, double Width, double Height) : TransitionShape
{
    public override double Area => Math.Max(0, Width) * Math.Max(0, Height);
}

public sealed record TransitionEllipse(double CenterX, double CenterY, double RadiusX, double RadiusY) : TransitionShape
{
    public override double Area => Math.PI * Math.Max(0, RadiusX) * Math.Max(0, RadiusY);
}

public sealed record TransitionPolygon(IReadOnlyList<TransitionPoint> Points) : TransitionShape
{
    public override double Area =>
        Math.Abs(Points.Select((point, index) =>
        {
            var next = Points[(index + 1) % Points.Count];
            return point.X * next.Y - next.X * point.Y;
        }).Sum()) / 2;
}

public readonly record struct TransitionPoint(double X, double Y);

public sealed record SlideLayerState
{
    public static readonly SlideLayerState Plain = new();

    public double Opacity { get; init; } = 1;

    public double OffsetX { get; init; }

    public double OffsetY { get; init; }

    public double ScaleX { get; init; } = 1;

    public double ScaleY { get; init; } = 1;

    public double Rotation { get; init; }

    public IReadOnlyList<TransitionShape>? Clip { get; init; }
}

public sealed record TransitionFrame
{
    public SlideLayerState Outgoing { get; init; } = SlideLayerState.Plain;

    public SlideLayerState Incoming { get; init; } = SlideLayerState.Plain;

    public bool OutgoingOnTop { get; init; }
}

public interface ITransitionEffect
{
    string Family { get; }

    bool Approximate { get; }

    TransitionFrame At(double progress);
}

public interface ITransitionEffectFactory
{
    ITransitionEffect Create(SmilTransition transition);
}

public abstract class TransitionEffect(string family, bool approximate = false) : ITransitionEffect
{
    public string Family => family;

    public bool Approximate => approximate;

    public TransitionFrame At(double progress)
    {
        var clamped = Math.Clamp(progress, 0, 1);
        return clamped >= 1 ? new TransitionFrame { Outgoing = SlideLayerState.Plain with { Opacity = 0 } } : Frame(clamped);
    }

    protected abstract TransitionFrame Frame(double progress);

    protected TransitionFrame Revealed(IReadOnlyList<TransitionShape> clip) =>
        new() { Incoming = SlideLayerState.Plain with { Clip = clip } };

    protected TransitionFrame Concealed(IReadOnlyList<TransitionShape> clip) =>
        new() { Outgoing = SlideLayerState.Plain with { Clip = clip }, OutgoingOnTop = true };
}

public sealed class CrossFadeEffect() : TransitionEffect("fade")
{
    protected override TransitionFrame Frame(double progress) =>
        new() { Incoming = SlideLayerState.Plain with { Opacity = progress } };
}

public sealed class FadeThroughColorEffect() : TransitionEffect("fade-through-color")
{
    protected override TransitionFrame Frame(double progress) => progress < 0.5
        ? new() { Outgoing = SlideLayerState.Plain with { Opacity = 1 - progress * 2 }, Incoming = SlideLayerState.Plain with { Opacity = 0 } }
        : new() { Outgoing = SlideLayerState.Plain with { Opacity = 0 }, Incoming = SlideLayerState.Plain with { Opacity = progress * 2 - 1 } };
}

public sealed class BarWipeEffect(double directionX, double directionY) : TransitionEffect("wipe")
{
    protected override TransitionFrame Frame(double progress)
    {
        var width = directionX == 0 ? 1 : progress;
        var height = directionY == 0 ? 1 : progress;
        var x = directionX < 0 ? 1 - progress : 0;
        var y = directionY < 0 ? 1 - progress : 0;
        return Revealed([new TransitionRectangle(x, y, width, height)]);
    }
}

public sealed class DiagonalWipeEffect(bool fromLeft, bool fromTop) : TransitionEffect("diagonal-wipe")
{
    protected override TransitionFrame Frame(double progress)
    {
        var reach = progress * 2;
        TransitionPoint Corner(double x, double y) => new(fromLeft ? x : 1 - x, fromTop ? y : 1 - y);
        return Revealed([new TransitionPolygon([Corner(0, 0), Corner(reach, 0), Corner(0, reach)])]);
    }
}

public sealed class BarnDoorEffect(bool vertical, bool inward) : TransitionEffect("split")
{
    protected override TransitionFrame Frame(double progress)
    {
        if (!inward)
        {
            var half = progress / 2;
            return Revealed([vertical ? new TransitionRectangle(0.5 - half, 0, progress, 1) : new TransitionRectangle(0, 0.5 - half, 1, progress)]);
        }

        var side = progress / 2;
        return Revealed(vertical
            ? [new TransitionRectangle(0, 0, side, 1), new TransitionRectangle(1 - side, 0, side, 1)]
            : [new TransitionRectangle(0, 0, 1, side), new TransitionRectangle(0, 1 - side, 1, side)]);
    }
}

public sealed class BlindsEffect(bool vertical, int count = 6) : TransitionEffect("blinds")
{
    protected override TransitionFrame Frame(double progress)
    {
        var band = 1.0 / count;
        return Revealed([.. Enumerable.Range(0, count).Select(index => vertical
            ? new TransitionRectangle(index * band, 0, band * progress, 1)
            : new TransitionRectangle(0, index * band, 1, band * progress))]);
    }
}

public sealed class CheckerboardEffect(bool down, int cells = 8) : TransitionEffect("checkerboard")
{
    protected override TransitionFrame Frame(double progress)
    {
        var size = 1.0 / cells;
        var shapes = new List<TransitionShape>();
        for (var row = 0; row < cells; row++)
        {
            for (var column = 0; column < cells; column++)
            {
                var shift = (down ? column : row) % 2 == 0 ? 0 : 1;
                var length = Math.Clamp(progress * 2 - shift, 0, 1) * size;
                if (length <= 0) continue;
                shapes.Add(down
                    ? new TransitionRectangle(column * size, row * size, size, length)
                    : new TransitionRectangle(column * size, row * size, length, size));
            }
        }

        return Revealed(shapes);
    }
}

public sealed class ShuffledCellsEffect(string family, int columns, int rows) : TransitionEffect(family)
{
    private readonly IReadOnlyList<int> _order = Shuffle(columns * rows);

    protected override TransitionFrame Frame(double progress)
    {
        var shown = (int)Math.Floor(progress * _order.Count);
        return Revealed([.. _order.Take(shown).Select(cell =>
            new TransitionRectangle(cell % columns / (double)columns, cell / columns / (double)rows, 1.0 / columns, 1.0 / rows))]);
    }

    private static IReadOnlyList<int> Shuffle(int count)
    {
        var random = new Random(count);
        var order = Enumerable.Range(0, count).ToArray();
        random.Shuffle(order);
        return order;
    }
}

public sealed class OrderedCellsEffect(string family, int columns, int rows, IReadOnlyList<int> order) : TransitionEffect(family)
{
    protected override TransitionFrame Frame(double progress)
    {
        var shown = (int)Math.Floor(progress * order.Count);
        return Revealed([.. order.Take(shown).Select(cell =>
            new TransitionRectangle(cell % columns / (double)columns, cell / columns / (double)rows, 1.0 / columns, 1.0 / rows))]);
    }
}

public sealed class IrisEffect(string family, IIrisOutline outline, bool shrinking) : TransitionEffect(family)
{
    protected override TransitionFrame Frame(double progress) =>
        shrinking ? Concealed(outline.At(1 - progress)) : Revealed(outline.At(progress));
}

public interface IIrisOutline
{
    IReadOnlyList<TransitionShape> At(double extent);
}

public sealed class RectangleOutline : IIrisOutline
{
    public IReadOnlyList<TransitionShape> At(double extent) =>
        [new TransitionRectangle(0.5 - extent / 2, 0.5 - extent / 2, extent, extent)];
}

public sealed class EllipseOutline(double ratioX, double ratioY) : IIrisOutline
{
    public IReadOnlyList<TransitionShape> At(double extent) =>
        [new TransitionEllipse(0.5, 0.5, extent * 0.75 * ratioX, extent * 0.75 * ratioY)];
}

public sealed class PolygonOutline(IReadOnlyList<TransitionPoint> unit, double reach) : IIrisOutline
{
    public IReadOnlyList<TransitionShape> At(double extent) =>
        [new TransitionPolygon([.. unit.Select(point => new TransitionPoint(0.5 + point.X * extent * reach, 0.5 + point.Y * extent * reach))])];
}

public sealed class CornersOutline : IIrisOutline
{
    public IReadOnlyList<TransitionShape> At(double extent)
    {
        var size = extent / 2;
        return
        [
            new TransitionRectangle(0, 0, size, size),
            new TransitionRectangle(1 - size, 0, size, size),
            new TransitionRectangle(0, 1 - size, size, size),
            new TransitionRectangle(1 - size, 1 - size, size, size),
        ];
    }
}

public sealed class SweepEffect(string family, int blades, double startAngle, bool clockwise, double centerX = 0.5, double centerY = 0.5, double span = 360) : TransitionEffect(family)
{
    protected override TransitionFrame Frame(double progress)
    {
        var sweep = span / blades * progress;
        var shapes = new List<TransitionShape>();
        for (var blade = 0; blade < blades; blade++)
        {
            var from = startAngle + blade * span / blades;
            shapes.Add(Sector(from, sweep));
        }

        return Revealed(shapes);
    }

    private TransitionPolygon Sector(double fromDegrees, double sweepDegrees)
    {
        var points = new List<TransitionPoint> { new(centerX, centerY) };
        var steps = Math.Max(2, (int)Math.Ceiling(sweepDegrees / 6));
        for (var step = 0; step <= steps; step++)
        {
            var angle = (fromDegrees + (clockwise ? 1 : -1) * sweepDegrees * step / steps - 90) * Math.PI / 180;
            points.Add(new TransitionPoint(centerX + Math.Cos(angle) * 1.5, centerY + Math.Sin(angle) * 1.5));
        }

        return new TransitionPolygon(points);
    }
}

public sealed class SlideCoverEffect(double fromX, double fromY, bool uncover) : TransitionEffect(uncover ? "uncover" : "cover")
{
    protected override TransitionFrame Frame(double progress) => uncover
        ? new() { Outgoing = SlideLayerState.Plain with { OffsetX = -fromX * progress, OffsetY = -fromY * progress }, OutgoingOnTop = true }
        : new() { Incoming = SlideLayerState.Plain with { OffsetX = fromX * (1 - progress), OffsetY = fromY * (1 - progress) } };
}

public sealed class PushEffect(double fromX, double fromY, bool approximate = false) : TransitionEffect("push", approximate)
{
    protected override TransitionFrame Frame(double progress) => new()
    {
        Incoming = SlideLayerState.Plain with { OffsetX = fromX * (1 - progress), OffsetY = fromY * (1 - progress) },
        Outgoing = SlideLayerState.Plain with { OffsetX = -fromX * progress, OffsetY = -fromY * progress },
    };
}

public sealed class ZoomEffect(bool rotating, bool outward) : TransitionEffect("zoom")
{
    protected override TransitionFrame Frame(double progress) => outward
        ? new()
        {
            Outgoing = SlideLayerState.Plain with { ScaleX = 1 + progress, ScaleY = 1 + progress, Opacity = 1 - progress },
            Incoming = SlideLayerState.Plain with { Opacity = progress },
            OutgoingOnTop = true,
        }
        : new()
        {
            Incoming = SlideLayerState.Plain with
            {
                ScaleX = progress, ScaleY = progress, Rotation = rotating ? (1 - progress) * 720 : 0, Opacity = Math.Min(1, progress * 2),
            },
        };
}

public sealed class FlipEffect(bool vertical) : TransitionEffect("3d", approximate: true)
{
    protected override TransitionFrame Frame(double progress)
    {
        var squeeze = Math.Abs(Math.Cos(Math.PI * progress));
        return progress < 0.5
            ? new()
            {
                Outgoing = SlideLayerState.Plain with { ScaleX = vertical ? 1 : squeeze, ScaleY = vertical ? squeeze : 1 },
                Incoming = SlideLayerState.Plain with { Opacity = 0 },
                OutgoingOnTop = true,
            }
            : new()
            {
                Outgoing = SlideLayerState.Plain with { Opacity = 0 },
                Incoming = SlideLayerState.Plain with { ScaleX = vertical ? 1 : squeeze, ScaleY = vertical ? squeeze : 1 },
            };
    }
}
