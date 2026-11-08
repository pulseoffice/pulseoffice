namespace PulseOffice.Foundation.Zoom;

public enum ViewZoomMode { FitWhole, FitWidth, Fixed }

public readonly record struct ViewZoom(ViewZoomMode Mode, double Factor)
{
    public static ViewZoom Whole => new(ViewZoomMode.FitWhole, 1);

    public static ViewZoom Width => new(ViewZoomMode.FitWidth, 1);

    public bool Matches(double factor) => Mode == ViewZoomMode.Fixed && Math.Abs(Factor - factor) < 0.005;
}

public sealed record ZoomLimits(double Minimum, double Maximum)
{
    public static readonly ZoomLimits Slides = new(0.1, 4);

    public static readonly ZoomLimits Pages = new(0.1, 5);

    public double Clamp(double factor) => double.IsFinite(factor) ? Math.Clamp(factor, Minimum, Maximum) : 1;

    public ViewZoom At(double factor) => new(ViewZoomMode.Fixed, Clamp(factor));

    public bool CanGrow(double factor) => factor < Maximum - 0.005;

    public bool CanShrink(double factor) => factor > Minimum + 0.005;
}

public sealed class ZoomHistory
{
    private readonly Stack<ViewZoom> _back = new();
    private readonly Stack<ViewZoom> _forward = new();

    public bool CanGoBack => _back.Count > 0;

    public bool CanGoForward => _forward.Count > 0;

    public void Record(ViewZoom previous, ViewZoom next)
    {
        if (previous == next) return;
        _back.Push(previous);
        _forward.Clear();
    }

    public ViewZoom? Back(ViewZoom current)
    {
        if (_back.Count == 0) return null;
        _forward.Push(current);
        return _back.Pop();
    }

    public ViewZoom? Forward(ViewZoom current)
    {
        if (_forward.Count == 0) return null;
        _back.Push(current);
        return _forward.Pop();
    }
}
