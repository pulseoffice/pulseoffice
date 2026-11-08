using Avalonia.Controls;
using PulseOffice.Foundation.Windowing;

namespace PulseOffice.Shell;

public sealed class WindowPlacementCoordinator(IWindowPlacementStore store, IPlacementPolicy policy, IPlacementPixelMapper mapper)
{
    public void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        WindowPlacement? normal = Restore(window);

        void Track()
        {
            if (window.WindowState == WindowState.Normal && window.IsVisible) normal = Current(window);
        }

        window.PositionChanged += (_, _) => Track();
        window.SizeChanged += (_, _) => Track();
        window.Closing += (_, _) => Remember(window, normal);
    }

    private WindowPlacement Restore(Window window)
    {
        var scaling = Scaling(window);
        var fallback = new WindowPlacement(0, 0, window.Width * scaling, window.Height * scaling, Maximized: false);
        var screens = Screens(window);

        var resolved = policy.Resolve(
            store.Load(), [.. screens.Select(screen => screen.Pixels)], fallback,
            window.MinWidth * scaling, window.MinHeight * scaling);

        var frame = mapper.ToWindow(resolved, screens, scaling);

        window.Width = frame.Width;
        window.Height = frame.Height;
        window.Position = new Avalonia.PixelPoint(frame.X, frame.Y);
        window.WindowStartupLocation = WindowStartupLocation.Manual;

        if (resolved.Maximized) window.WindowState = WindowState.Maximized;

        return resolved with { Maximized = false };
    }

    private void Remember(Window window, WindowPlacement? normal)
    {
        var maximized = window.WindowState is WindowState.Maximized or WindowState.FullScreen;

        var bounds = maximized ? normal ?? Current(window) : Current(window);

        store.Save(bounds with { Maximized = maximized });
    }

    private WindowPlacement Current(Window window) =>
        mapper.FromWindow(window.Position.X, window.Position.Y, window.Bounds.Width, window.Bounds.Height, Scaling(window));

    private double Scaling(Window window) =>
        window.Screens?.ScreenFromWindow(window)?.Scaling
        ?? window.Screens?.Primary?.Scaling
        ?? 1;

    private IReadOnlyList<PlacementScreen> Screens(Window window) =>
        window.Screens is not { } screens
            ? []
            : [.. screens.All.Select(screen => new PlacementScreen(
                new ScreenArea(screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Width, screen.WorkingArea.Height),
                screen.Scaling))];
}
