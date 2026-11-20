using Avalonia;
using PulseOffice.Foundation.Windowing;

namespace PulseOffice.Shell;

public interface ITornOffWindowPlacementStore
{
    WindowPlacement? Load();

    void Save(WindowPlacement placement);
}

public sealed class TornOffWindowPlacementStore(IWindowPlacementStore inner) : ITornOffWindowPlacementStore
{
    private readonly IWindowPlacementStore _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public WindowPlacement? Load() => _inner.Load();

    public void Save(WindowPlacement placement) => _inner.Save(placement);
}

public interface ITornOffWindowSizing
{
    Size Choose(WindowPlacement? saved, double scaling, Size source, Size minimum, Size? workingArea);

    bool OpensMaximized(WindowPlacement? saved);

    WindowPlacement Remember(WindowPlacement? normal, WindowPlacement current, bool maximized);
}

public sealed class TornOffWindowSizing : ITornOffWindowSizing
{
    public Size Choose(WindowPlacement? saved, double scaling, Size source, Size minimum, Size? workingArea)
    {
        var factor = scaling > 0 && double.IsFinite(scaling) ? scaling : 1;
        var wanted = saved is { } placement && placement.Width > 0 && placement.Height > 0
            ? new Size(placement.Width / factor, placement.Height / factor)
            : source;
        var width = Math.Max(minimum.Width, wanted.Width);
        var height = Math.Max(minimum.Height, wanted.Height);
        if (workingArea is { } area && area.Width > 0 && area.Height > 0)
        {
            width = Math.Max(minimum.Width, Math.Min(width, area.Width));
            height = Math.Max(minimum.Height, Math.Min(height, area.Height));
        }

        return new Size(width, height);
    }

    public bool OpensMaximized(WindowPlacement? saved) => saved is { Maximized: true };

    public WindowPlacement Remember(WindowPlacement? normal, WindowPlacement current, bool maximized) =>
        (maximized ? normal ?? current : current) with { Maximized = maximized };
}
