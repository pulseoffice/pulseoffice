using PulseOffice.Document.Tables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using PulseOffice.Localization;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Icons;
using PulseOffice.UI.Theming;
using System;
using System.Linq;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations.UI;

internal sealed class DeckAnimationPlayer(DeckViewState state, IAnimationScheduler scheduler)
{
    private readonly DeckViewState _state = state;
    private readonly SlideView _canvas = state.Canvas;
    private readonly IAnimationScheduler _scheduler = scheduler;

    private Avalonia.Threading.DispatcherTimer? _playTimer;
    private System.Diagnostics.Stopwatch? _playClock;
    private IReadOnlyList<ScheduledEffect> _playSchedule = [];

    public void TogglePlay()
    {
        if (_playTimer is not null) { StopPlay(); return; }
        if (_state.Current is not { } slide || slide.Animations.Count == 0) return;

        _playSlide = slide;
        _playSchedule = _scheduler.Schedule(slide.Animations);
        var total = _scheduler.Duration(_playSchedule) + 400;

        _playClock = System.Diagnostics.Stopwatch.StartNew();
        _canvas.PlaybackVisual = id => _scheduler.VisualAt(id, _playSchedule, (int)_playClock.ElapsedMilliseconds);

        _playTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _playTimer.Tick += (_, _) =>
        {
            _canvas.InvalidateVisual();
            if (_playClock!.ElapsedMilliseconds >= total) StopPlay();
        };
        _playTimer.Start();
    }

    private Slide? _playSlide;

    public void StopPlay()
    {
        _playSlide = null;
        _playTimer?.Stop();
        _playTimer = null;
        _playClock = null;
        _canvas.PlaybackVisual = null;
        _canvas.InvalidateVisual();
    }

    public bool PlaysOther(Slide? slide) => _playTimer is not null && !ReferenceEquals(_playSlide, slide);
}
