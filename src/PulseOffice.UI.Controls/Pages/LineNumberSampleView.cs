using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PulseOffice.UI.Controls.Pages;

public sealed record LineNumberSample(bool On, int Every, bool Right, double DistancePoints, bool CountBlank);

public readonly record struct NumberedLine(double Y, double Width, string? Number, double NumberX, bool Blank);

public interface ILineNumberSampleLayout
{
    IReadOnlyList<NumberedLine> Lines(LineNumberSample sample, double width, double height);
}

public sealed class LineNumberSampleLayout : ILineNumberSampleLayout
{
    private const double Pitch = 11;
    private const double Margin = 34;
    private const double Scale = 0.6;

    private readonly double[] _lengths = [0.95, 0.9, 0.97, 0.6, 0, 0.92, 0.96, 0.88, 0.94, 0.5, 0, 0.9, 0.97, 0.85];

    public IReadOnlyList<NumberedLine> Lines(LineNumberSample sample, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var column = Math.Max(20, width - 2 * Margin);
        var lines = new List<NumberedLine>();
        var counter = 0;
        var y = 10.0;
        foreach (var length in _lengths)
        {
            if (y + Pitch > height) break;
            var blank = length == 0;
            if (!blank || sample.CountBlank) counter++;
            var every = Math.Max(1, sample.Every);
            var numbered = sample.On && (!blank || sample.CountBlank) && counter % every == 0;
            var gap = Math.Clamp(sample.DistancePoints * Scale, 2, Margin - 4);
            var x = sample.Right ? Margin + column + gap : Margin - gap;
            lines.Add(new NumberedLine(y, column * length, numbered ? counter.ToString(CultureInfo.InvariantCulture) : null, x, blank));
            y += Pitch;
        }
        return lines;
    }
}

public sealed class LineNumberSampleView : Control
{
    private readonly ILineNumberSampleLayout _layout;
    private readonly IBrush _bar = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
    private readonly IBrush _number = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
    private LineNumberSample _sample = new(true, 5, false, 14, true);

    public LineNumberSampleView(ILineNumberSampleLayout layout)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Width = 240;
        Height = 170;
    }

    public LineNumberSample Sample => _sample;

    public void Show(LineNumberSample sample)
    {
        _sample = sample ?? throw new ArgumentNullException(nameof(sample));
        InvalidateVisual();
    }

    public IReadOnlyList<NumberedLine> Lines() => _layout.Lines(_sample, Width, Height);

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));
        foreach (var line in Lines())
        {
            if (!line.Blank) context.FillRectangle(_bar, new Rect(34, line.Y, line.Width, 4), 1.5f);
            if (line.Number is not { } number) continue;
            var text = new FormattedText(number, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 8, _number);
            var x = _sample.Right ? line.NumberX : line.NumberX - text.Width;
            context.DrawText(text, new Point(x, line.Y + 2 - text.Height / 2));
        }
    }
}
