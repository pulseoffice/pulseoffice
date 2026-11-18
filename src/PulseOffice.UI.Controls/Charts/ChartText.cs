using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace PulseOffice.UI.Controls.Charts;

public sealed record ChartInk(IBrush Text, IBrush Paper)
{
    public static readonly ChartInk Print = new(Brushes.Black, Brushes.White);

    public IBrush Muted => Text is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, 0.5 * solid.Opacity) : Brushes.Gray;

    public IBrush Backdrop => Paper is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, 0.7 * solid.Opacity) : new SolidColorBrush(Colors.White, 0.7);
}

public sealed class ChartText(CultureInfo culture, ChartInk? ink = null)
{
    public CultureInfo Culture => culture;

    public ChartInk Ink { get; } = ink ?? ChartInk.Print;

    public FormattedText Layout(string text, IBrush brush, double size, bool bold = false) =>
        new(text, culture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal),
            size, brush);

    public string Number(double value) => Number(value, Math.Abs(value) >= 1000 ? "#,##0" : "0.##");

    public string Number(double value, string pattern) =>
        double.IsFinite(value) ? value.ToString(pattern, culture) : string.Empty;

    public string Formatted(double value, string? code)
    {
        if (code is not { Length: > 0 } || code.Equals("General", StringComparison.OrdinalIgnoreCase) || code.Contains('[')) return Number(value);
        try
        {
            return Number(value, code);
        }
        catch (FormatException)
        {
            return Number(value);
        }
    }

    public void Centred(DrawingContext context, FormattedText text, Point centre) =>
        context.DrawText(text, new Point(centre.X - text.Width / 2, centre.Y - text.Height / 2));
}
