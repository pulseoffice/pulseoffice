using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.TextFormat;

public readonly record struct SampleLine(double X, double Y, double Width, bool Current);

public interface IParagraphSampleLayout
{
    IReadOnlyList<SampleLine> Lines(ParagraphFormatState state, double width, double height);
}

public sealed class ParagraphSampleLayout : IParagraphSampleLayout
{
    private const double ColumnPoints = 432;
    private const double Pitch = 9;
    private const double Stroke = 4;
    private const double Margin = 10;

    private readonly double[] _lengths = [0.94, 0.98, 0.9, 0.96, 0.58];
    private readonly double[] _context = [0.97, 0.93, 0.62];

    public IReadOnlyList<SampleLine> Lines(ParagraphFormatState state, double width, double height)
    {
        ArgumentNullException.ThrowIfNull(state);
        var column = Math.Max(20, width - 2 * Margin);
        var scale = column / ColumnPoints;
        var lines = new List<SampleLine>();
        var y = Margin;
        foreach (var length in _context)
        {
            lines.Add(new SampleLine(Margin, y, column * length, false));
            y += Pitch;
        }

        y += Math.Min(40, state.Before * scale * 1.6) + Pitch * 0.4;
        var pitch = CurrentPitch(state, scale);
        var left = Margin + Math.Clamp(state.Left * scale, -Margin, column * 0.6);
        var right = Margin + column - Math.Clamp(state.Right * scale, -Margin, column * 0.6);
        for (var index = 0; index < _lengths.Length; index++)
        {
            var start = left + (index == 0 ? Math.Clamp(state.FirstLine * scale, -(left - 2), column * 0.4) : 0);
            var available = Math.Max(8, right - start);
            var last = index == _lengths.Length - 1;
            var length = state.Align == TextAlign.Justify && !last ? available : available * _lengths[index];
            var x = state.Align switch
            {
                TextAlign.Center => start + (available - length) / 2,
                TextAlign.Right => right - length,
                _ => start,
            };
            lines.Add(new SampleLine(x, y, length, true));
            y += pitch;
        }

        y += Math.Min(40, state.After * scale * 1.6) + Pitch * 0.4 - (pitch - Pitch);
        foreach (var length in _context)
        {
            if (y + Stroke > height) break;
            lines.Add(new SampleLine(Margin, y, column * length, false));
            y += Pitch;
        }
        return lines;
    }

    private double CurrentPitch(ParagraphFormatState state, double scale) => state.LineMode switch
    {
        LineHeightMode.Proportional => Pitch * Math.Clamp(state.LineValue / 100, 0.6, 4),
        LineHeightMode.Exact => Math.Clamp(state.LineValue * scale * 1.6, Stroke + 1, Pitch * 4),
        LineHeightMode.AtLeast => Math.Max(Pitch, Math.Min(Pitch * 4, state.LineValue * scale * 1.6)),
        _ => Pitch + Math.Clamp(state.LineValue * scale * 1.6, 0, Pitch * 3),
    };
}

public sealed class ParagraphSampleView : Control
{
    private readonly IParagraphSampleLayout _layout;
    private readonly IBrush _context = new SolidColorBrush(Color.FromRgb(0xD4, 0xD8, 0xDE));
    private readonly IBrush _current = new SolidColorBrush(Color.FromRgb(0x4B, 0x55, 0x63));
    private ParagraphFormatState _state = new();

    public ParagraphSampleView(IParagraphSampleLayout layout)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        Width = 240;
        Height = 180;
    }

    public ParagraphFormatState State => _state;

    public void Show(ParagraphFormatState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        InvalidateVisual();
    }

    public IReadOnlyList<SampleLine> Lines() => _layout.Lines(_state, Width, Height);

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));
        using (context.PushClip(new Rect(Bounds.Size)))
        {
            foreach (var line in Lines())
                context.FillRectangle(line.Current ? _current : _context, new Rect(line.X, line.Y, Math.Max(0, line.Width), 4), 1.5f);
        }
    }
}
