namespace PulseOffice.Presentations;

public sealed class TransitionEffectFactory : ITransitionEffectFactory
{
    private readonly IReadOnlyDictionary<string, (double X, double Y)> _origins = new Dictionary<string, (double X, double Y)>(StringComparer.OrdinalIgnoreCase)
    {
        ["fromLeft"] = (-1, 0),
        ["fromRight"] = (1, 0),
        ["fromTop"] = (0, -1),
        ["fromBottom"] = (0, 1),
        ["fromTopLeft"] = (-1, -1),
        ["fromTopRight"] = (1, -1),
        ["fromBottomLeft"] = (-1, 1),
        ["fromBottomRight"] = (1, 1),
        ["leftToRight"] = (-1, 0),
        ["rightToLeft"] = (1, 0),
        ["topToBottom"] = (0, -1),
        ["bottomToTop"] = (0, 1),
    };

    public ITransitionEffect Create(SmilTransition transition)
    {
        var type = transition.Type;
        var subtype = transition.Subtype;
        var reverse = transition.Reverse;
        return type switch
        {
            _ when Is(subtype, "fadeOverColor") || Is(subtype, "fadeToColor") || Is(subtype, "fadeFromColor") => new FadeThroughColorEffect(),
            "fade" => new CrossFadeEffect(),
            "dissolve" => new ShuffledCellsEffect("dissolve", 32, 24),
            "barWipe" => Bar(subtype, reverse),
            "boxWipe" or "veeWipe" or "zigZagWipe" or "barnZigZagWipe" or "barnVeeWipe" => Bar(subtype, reverse, approximate: true),
            "barnDoorWipe" => Is(subtype, "diagonalBottomLeft") || Is(subtype, "diagonalTopLeft")
                ? new IrisEffect("split", new PolygonOutline([new(0, -1), new(1, 0), new(0, 1), new(-1, 0)], 0.75), reverse)
                : new BarnDoorEffect(Is(subtype, "vertical"), reverse),
            "blindsWipe" => new BlindsEffect(Is(subtype, "vertical")),
            "checkerBoardWipe" => new CheckerboardEffect(Is(subtype, "down")),
            "randomBarWipe" => Is(subtype, "vertical") ? new ShuffledCellsEffect("random-bars", 64, 1) : new ShuffledCellsEffect("random-bars", 1, 48),
            "irisWipe" => Is(subtype, "diamond")
                ? new IrisEffect("diamond", new PolygonOutline([new(0, -1), new(1, 0), new(0, 1), new(-1, 0)], 1.0), reverse)
                : new IrisEffect("box", new RectangleOutline(), reverse),
            "ellipseWipe" => new IrisEffect("circle", Is(subtype, "vertical") ? new EllipseOutline(0.7, 1) : Is(subtype, "horizontal") ? new EllipseOutline(1, 0.7) : new EllipseOutline(1, 1), reverse),
            "eyeWipe" or "roundRectWipe" => new IrisEffect("circle", new EllipseOutline(1, 0.5), reverse),
            "starWipe" => new IrisEffect("star", new PolygonOutline(Star(5), 1.2), reverse),
            "triangleWipe" or "arrowHeadWipe" or "pentagonWipe" or "hexagonWipe" => new IrisEffect("shape", new PolygonOutline(Star(3), 1.4), reverse),
            "fourBoxWipe" => Is(subtype, "cornersOut")
                ? new IrisEffect("plus", new PolygonOutline(Plus(), 1.0), reverse)
                : new IrisEffect("corners", new CornersOutline(), reverse),
            "miscDiagonalWipe" => new BarnDoorEffect(Is(subtype, "doubleBarnDoor"), reverse),
            "clockWipe" => new SweepEffect("clock", 1, Start(subtype), !Is(subtype, "counterClockwiseTwelve") && !reverse),
            "pinWheelWipe" => new SweepEffect("wheel", Blades(subtype), 0, !reverse),
            "singleSweepWipe" or "doubleSweepWipe" or "saloonDoorWipe" or "windshieldWipe" => new SweepEffect("sweep", 2, 0, !reverse),
            "fanWipe" or "doubleFanWipe" => new SweepEffect("wedge", 2, 0, !reverse, 0.5, 0.5, 180),
            "waterfallWipe" => new DiagonalWipeEffect(Is(subtype, "horizontalLeft") != reverse, true),
            "snakeWipe" or "boxSnakesWipe" or "parallelSnakesWipe" => new OrderedCellsEffect("snake", 8, 6, Snake(8, 6, reverse)),
            "spiralWipe" => new OrderedCellsEffect("spiral", 8, 6, Spiral(8, 6, reverse)),
            "slideWipe" => new SlideCoverEffect(Origin(subtype).X, Origin(subtype).Y, reverse),
            "pushWipe" when Is(subtype, "combHorizontal") || Is(subtype, "combVertical") =>
                new PushEffect(Is(subtype, "combHorizontal") ? 1 : 0, Is(subtype, "combVertical") ? 1 : 0, approximate: true),
            "pushWipe" => new PushEffect(Origin(subtype).X * (reverse ? -1 : 1), Origin(subtype).Y * (reverse ? -1 : 1)),
            "zoom" => new ZoomEffect(Is(subtype, "rotateIn"), Is(subtype, "rotateOut") || reverse),
            "miscShapeWipe" or "spinWipe" or "turnAround" or "rochade" => new FlipEffect(Is(subtype, "vertical") || Is(subtype, "topToBottom")),
            "random" => new ShuffledCellsEffect("random", 16, 12),
            _ => new CrossFadeEffect(),
        };
    }

    private ITransitionEffect Bar(string subtype, bool reverse, bool approximate = false)
    {
        if (Is(subtype, "fromTopLeft") || Is(subtype, "fromBottomRight") || Is(subtype, "fromTopRight") || Is(subtype, "fromBottomLeft"))
            return new DiagonalWipeEffect(subtype.Contains("Left", StringComparison.Ordinal) != reverse, subtype.Contains("Top", StringComparison.Ordinal) != reverse);
        var (x, y) = Origin(subtype);
        if (x == 0 && y == 0) (x, y) = (-1, 0);
        return approximate
            ? new IrisEffect("wipe", new RectangleOutline(), reverse)
            : new BarWipeEffect(reverse ? x : -x, reverse ? y : -y);
    }

    private (double X, double Y) Origin(string subtype) =>
        _origins.TryGetValue(subtype, out var origin) ? origin : (1, 0);

    private bool Is(string value, string expected) => string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private int Blades(string subtype) => subtype switch
    {
        "oneBlade" => 1,
        "twoBladeVertical" or "twoBladeHorizontal" => 2,
        "threeBlade" => 3,
        "fourBlade" => 4,
        "eightBlade" => 8,
        _ => 4,
    };

    private double Start(string subtype) => subtype switch
    {
        "clockwiseThree" => 90,
        "clockwiseSix" => 180,
        "clockwiseNine" => 270,
        _ => 0,
    };

    private IReadOnlyList<TransitionPoint> Star(int tips) =>
        [.. Enumerable.Range(0, tips * 2).Select(index =>
        {
            var radius = index % 2 == 0 ? 1.0 : tips == 3 ? 1.0 : 0.45;
            var angle = Math.PI * index / tips - Math.PI / 2;
            return new TransitionPoint(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
        })];

    private IReadOnlyList<TransitionPoint> Plus() =>
    [
        new(-0.3, -1), new(0.3, -1), new(0.3, -0.3), new(1, -0.3), new(1, 0.3), new(0.3, 0.3),
        new(0.3, 1), new(-0.3, 1), new(-0.3, 0.3), new(-1, 0.3), new(-1, -0.3), new(-0.3, -0.3),
    ];

    private IReadOnlyList<int> Snake(int columns, int rows, bool reverse)
    {
        var order = Enumerable.Range(0, rows)
            .SelectMany(row => Enumerable.Range(0, columns).Select(column => row * columns + (row % 2 == 0 ? column : columns - 1 - column)))
            .ToList();
        if (reverse) order.Reverse();
        return order;
    }

    private IReadOnlyList<int> Spiral(int columns, int rows, bool reverse)
    {
        var order = new List<int>();
        int top = 0, bottom = rows - 1, left = 0, right = columns - 1;
        while (top <= bottom && left <= right)
        {
            for (var column = left; column <= right; column++) order.Add(top * columns + column);
            for (var row = top + 1; row <= bottom; row++) order.Add(row * columns + right);
            if (top < bottom) for (var column = right - 1; column >= left; column--) order.Add(bottom * columns + column);
            if (left < right) for (var row = bottom - 1; row > top; row--) order.Add(row * columns + left);
            top++;
            bottom--;
            left++;
            right--;
        }

        if (reverse) order.Reverse();
        return order;
    }
}
