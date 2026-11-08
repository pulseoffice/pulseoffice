using System.Text.Json.Serialization;

namespace PulseOffice.Foundation.Windowing;

public readonly record struct ScreenArea(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public double OverlapWith(WindowPlacement placement)
    {
        var width = Math.Min(Right, placement.Right) - Math.Max(X, placement.X);
        var height = Math.Min(Bottom, placement.Bottom) - Math.Max(Y, placement.Y);

        return width <= 0 || height <= 0 ? 0 : width * height;
    }
}

public readonly record struct WindowPlacement(double X, double Y, double Width, double Height, bool Maximized)
{
    [JsonIgnore]
    public double Right => X + Width;

    [JsonIgnore]
    public double Bottom => Y + Height;

    [JsonIgnore]
    public double Area => Width * Height;
}

public interface IPlacementPolicy
{
    WindowPlacement Resolve(
        WindowPlacement? saved,
        IReadOnlyList<ScreenArea> screens,
        WindowPlacement fallback,
        double minimumWidth,
        double minimumHeight);
}

public sealed class PlacementPolicy : IPlacementPolicy
{
    private const double VisibleFraction = 0.4;

    public WindowPlacement Resolve(
        WindowPlacement? saved,
        IReadOnlyList<ScreenArea> screens,
        WindowPlacement fallback,
        double minimumWidth,
        double minimumHeight)
    {
        ArgumentNullException.ThrowIfNull(screens);

        var unplaced = saved is not { } stored || !IsSane(stored, minimumWidth, minimumHeight);
        var wanted = unplaced ? fallback : saved!.Value;

        if (screens.Count == 0) return wanted;

        var screen = Best(wanted, screens);

        var width = Fit(wanted.Width, minimumWidth, screen.Width);
        var height = Fit(wanted.Height, minimumHeight, screen.Height);

        var sized = wanted with { Width = width, Height = height };

        return !unplaced && screen.OverlapWith(sized) >= sized.Area * VisibleFraction
            ? sized with { X = Nudge(sized.X, width, screen.X, screen.Right), Y = Nudge(sized.Y, height, screen.Y, screen.Bottom) }
            : sized with
            {
                X = screen.X + ((screen.Width - width) / 2),
                Y = screen.Y + ((screen.Height - height) / 2),
            };
    }

    private double Fit(double wanted, double minimum, double available) =>
        Math.Max(minimum, Math.Min(wanted, available));

    private bool IsSane(WindowPlacement placement, double minimumWidth, double minimumHeight) =>
        double.IsFinite(placement.X) && double.IsFinite(placement.Y)
        && double.IsFinite(placement.Width) && double.IsFinite(placement.Height)
        && placement.Width >= minimumWidth && placement.Height >= minimumHeight;

    private ScreenArea Best(WindowPlacement placement, IReadOnlyList<ScreenArea> screens)
    {
        var best = screens[0];
        var bestOverlap = best.OverlapWith(placement);

        foreach (var screen in screens.Skip(1))
        {
            var overlap = screen.OverlapWith(placement);
            if (overlap <= bestOverlap) continue;

            best = screen;
            bestOverlap = overlap;
        }

        return best;
    }

    private double Nudge(double position, double length, double lower, double upper) =>
        length >= upper - lower ? lower : Math.Clamp(position, lower, upper - length);
}
