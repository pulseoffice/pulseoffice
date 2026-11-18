using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace PulseOffice.Presentations.UI;

public sealed class ShowRehearsalClock
{
    private readonly RehearsalTimeline _timeline = new();
    private readonly System.Diagnostics.Stopwatch _slideClock = new();
    private readonly System.Diagnostics.Stopwatch _totalClock = new();
    private readonly TextBlock _label = new()
    {
        FontSize = 22,
        FontWeight = FontWeight.SemiBold,
        Foreground = new SolidColorBrush(Color.FromRgb(236, 238, 244)),
    };
    private string? _slideId;
    private DispatcherTimer? _tick;

    public ShowRehearsalClock()
    {
        Overlay = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 26)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 8),
            Margin = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = _label,
            ZIndex = 20,
        };
    }

    public Control Overlay { get; }

    public void Start(string? slideId)
    {
        _totalClock.Restart();
        Mark(slideId);
        _tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _tick.Tick += (_, _) => Refresh();
        _tick.Start();
        Refresh();
    }

    public void Mark(string? slideId)
    {
        if (slideId == _slideId) return;

        if (_slideId is { } previous) _timeline.Record(previous, (int)_slideClock.ElapsedMilliseconds);
        _slideId = slideId;
        _slideClock.Restart();
    }

    public IReadOnlyDictionary<string, int> Finish()
    {
        _tick?.Stop();
        if (_slideId is { } id) _timeline.Record(id, (int)_slideClock.ElapsedMilliseconds);
        return _timeline.Times;
    }

    private void Refresh() =>
        _label.Text = $"{Clock(_slideClock.Elapsed)}   /   {Clock(_totalClock.Elapsed)}";

    private static string Clock(TimeSpan span) => $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
}
