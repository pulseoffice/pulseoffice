using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace PulseOffice.UI.Controls.Charts;

public sealed class ChartText(CultureInfo culture)
{
    public CultureInfo Culture => culture;

    public FormattedText Layout(string text, IBrush brush, double size, bool bold = false) =>
        new(text, culture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal),
            size, brush);

    public string Number(double value) => Number(value, Math.Abs(value) >= 1000 ? "#,##0" : "0.##");

    public string Number(double value, string pattern) =>
        double.IsFinite(value) ? value.ToString(pattern, culture) : string.Empty;

    public void Centred(DrawingContext context, FormattedText text, Point centre) =>
        context.DrawText(text, new Point(centre.X - text.Width / 2, centre.Y - text.Height / 2));
}
