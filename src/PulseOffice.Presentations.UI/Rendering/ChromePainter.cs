using Avalonia;
using Avalonia.Media;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI.Rendering;

public sealed class ChromePainter
{
    private readonly IBrush _darkInk = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromRgb(80, 80, 88));
    private readonly IBrush _lightInk = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromRgb(200, 200, 210));

    public void Paint(DrawingContext context, SlideChrome chrome, Rect target, Theme theme, Color background)
    {
        if (chrome.IsEmpty) return;

        var luminance = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255.0;
        var ink = luminance > 0.5 ? _darkInk : _lightInk;
        var band = new ChromeBand(context, target, theme, ink);

        if (chrome.Date is { } date) band.Draw(date, TextAlignment.Left);
        if (chrome.Footer is { } footer) band.Draw(footer, TextAlignment.Center);
        if (chrome.Number is { } number) band.Draw(number, TextAlignment.Right);
    }

    private sealed class ChromeBand(DrawingContext context, Rect target, Theme theme, IBrush ink)
    {
        private readonly double _fontSize = Math.Max(7, target.Height * 0.030);
        private readonly double _margin = target.Width * 0.035;
        private readonly double _top = target.Bottom - target.Height * 0.06;
        private readonly double _height = target.Height * 0.05;

        public void Draw(string text, TextAlignment align)
        {
            var formatted = new FormattedText(text, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface(theme.Fonts.Minor), _fontSize, ink);
            var x = align switch
            {
                TextAlignment.Right => target.Right - _margin - formatted.Width,
                TextAlignment.Center => target.X + (target.Width - formatted.Width) / 2,
                _ => target.X + _margin,
            };
            context.DrawText(formatted, new Point(x, _top + (_height - formatted.Height) / 2));
        }
    }
}
