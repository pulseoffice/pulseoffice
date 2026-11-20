using Avalonia;

namespace PulseOffice.Shell;

public sealed class TabStripDragGeometry(double edgeBand = 24, double maximumStep = 16, double tearOffDistance = 24)
{
    public double ScrollStep(double pointerX, double viewportWidth)
    {
        if (viewportWidth <= 0) return 0;
        var band = Math.Min(edgeBand, viewportWidth / 4);
        if (band <= 0) return 0;
        if (pointerX < band) return -maximumStep * Math.Min(1, (band - pointerX) / band);
        if (pointerX > viewportWidth - band) return maximumStep * Math.Min(1, (pointerX - (viewportWidth - band)) / band);
        return 0;
    }

    public double ScrolledOffset(double offset, double step, double extentWidth, double viewportWidth) =>
        Math.Clamp(offset + step, 0, Math.Max(0, extentWidth - viewportWidth));

    public double ContentX(double viewportX, double scrollOffset) => viewportX + scrollOffset;

    public bool IsTornOff(Point pointer, Size strip) =>
        pointer.Y < -tearOffDistance || pointer.Y > strip.Height + tearOffDistance;
}
