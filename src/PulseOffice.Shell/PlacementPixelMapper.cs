using PulseOffice.Foundation.Windowing;

namespace PulseOffice.Shell;

public readonly record struct PlacementScreen(ScreenArea Pixels, double Scaling);

public readonly record struct WindowFrame(int X, int Y, double Width, double Height);

public interface IPlacementPixelMapper
{
    WindowPlacement FromWindow(int x, int y, double width, double height, double scaling);

    WindowFrame ToWindow(WindowPlacement pixels, IReadOnlyList<PlacementScreen> screens, double fallbackScaling);
}

public sealed class PlacementPixelMapper : IPlacementPixelMapper
{
    public WindowPlacement FromWindow(int x, int y, double width, double height, double scaling) =>
        new(x, y, width * Usable(scaling), height * Usable(scaling), Maximized: false);

    public WindowFrame ToWindow(WindowPlacement pixels, IReadOnlyList<PlacementScreen> screens, double fallbackScaling)
    {
        ArgumentNullException.ThrowIfNull(screens);

        var scaling = Usable(fallbackScaling);
        var bestOverlap = 0.0;
        foreach (var screen in screens)
        {
            var overlap = screen.Pixels.OverlapWith(pixels);
            if (overlap <= bestOverlap) continue;

            bestOverlap = overlap;
            scaling = Usable(screen.Scaling);
        }

        return new WindowFrame((int)Math.Round(pixels.X), (int)Math.Round(pixels.Y), pixels.Width / scaling, pixels.Height / scaling);
    }

    private double Usable(double scaling) => double.IsFinite(scaling) && scaling > 0 ? scaling : 1;
}
