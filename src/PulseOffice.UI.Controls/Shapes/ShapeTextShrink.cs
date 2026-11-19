using Avalonia.Media;

namespace PulseOffice.UI.Controls.Shapes;

public interface IShapeTextShrink
{
    double Fit(FormattedText text, double width, double height, bool wrap);
}

public sealed class ShapeTextShrink : IShapeTextShrink
{
    private const double SmallestScale = 0.25;

    private const int Steps = 8;

    public double Fit(FormattedText text, double width, double height, bool wrap)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (width <= 0 || height <= 0 || Fits(text, width, height, wrap, 1)) return Settle(text, width, wrap, 1);

        var low = SmallestScale;
        var high = 1.0;
        if (!Fits(text, width, height, wrap, low)) return Settle(text, width, wrap, low);
        for (var step = 0; step < Steps; step++)
        {
            var middle = (low + high) / 2;
            if (Fits(text, width, height, wrap, middle)) low = middle;
            else high = middle;
        }
        return Settle(text, width, wrap, low);
    }

    private bool Fits(FormattedText text, double width, double height, bool wrap, double scale)
    {
        Settle(text, width, wrap, scale);
        return text.Height * scale <= height + 0.01 && (wrap || text.WidthIncludingTrailingWhitespace * scale <= width + 0.01);
    }

    private double Settle(FormattedText text, double width, bool wrap, double scale)
    {
        text.MaxTextWidth = wrap ? width / scale : double.PositiveInfinity;
        return scale;
    }
}
