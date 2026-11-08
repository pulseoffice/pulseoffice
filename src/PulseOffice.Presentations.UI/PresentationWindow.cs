using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public sealed class PresentationWindow : Window
{
    private readonly Presentation _deck;
    private readonly IReadOnlyList<Slide> _playlist;
    private readonly IAnimationScheduler _scheduler;
    private readonly PresentationController _controller;
    private readonly ISlideRenderer _renderer;
    private readonly bool _loop;
    private readonly bool _withoutAnimation;
    private readonly bool _manualAdvance;
    private readonly IBrush _penBrush;
    private readonly int _monitorIndex;

    private readonly SlideView _front;
    private readonly SlideView _back;
    private readonly Panel _stage;
    private readonly TransitionStage _transitions = new();

    private readonly Canvas _ink = new() { IsHitTestVisible = false };
    private readonly Ellipse _laser = new()
    {
        Width = 18,
        Height = 18,
        IsVisible = false,
        IsHitTestVisible = false,
    };

    private enum PointerTool { None, Pen, Laser }

    private PointerTool _tool = PointerTool.None;
    private Polyline? _stroke;
    private int _inkSlideIndex = -1;

    private readonly bool _rehearse;
    private readonly RehearsalTimeline _timeline = new();
    private readonly System.Diagnostics.Stopwatch _slideClock = new();
    private readonly System.Diagnostics.Stopwatch _totalClock = new();
    private string? _rehearsalId;
    private TextBlock? _timerLabel;
    private DispatcherTimer? _timerTick;

    private DispatcherTimer? _animator;
    private Action? _finishAnimation;
    private DispatcherTimer? _autoAdvance;
    private bool _busy;

    public event EventHandler? SlideChanged;

    public event EventHandler<IReadOnlyDictionary<string, int>>? RehearsalFinished;

    public Slide? CurrentSlide => _controller.Current;

    public Slide? NextSlide => _controller.SlideIndex + 1 < _playlist.Count
        ? _playlist[_controller.SlideIndex + 1]
        : null;

    public int SlideNumber => _controller.SlideIndex + 1;

    public int SlideTotal => _playlist.Count;

    private readonly PulseOffice.Document.Forms.IFormControlInteraction? _forms;
    private readonly Action<Slide, SlideElement>? _controlChanged;

    private readonly Func<MediaElement, Task>? _playMedia;

    public PresentationWindow(Presentation deck, IAnimationScheduler scheduler, ISlideRenderer renderer, PresentationOptions options)
    {
        _deck = deck;
        _playlist = [.. options.Playlist is { Count: > 0 } list ? list : deck.Slides];
        _scheduler = scheduler;
        _renderer = renderer;
        _loop = options.Loop;
        _rehearse = options.Rehearse;
        _withoutAnimation = options.WithoutAnimation;
        _manualAdvance = options.ManualAdvance;
        _monitorIndex = options.MonitorIndex;
        _forms = options.Forms;
        _controlChanged = options.ControlChanged;
        _playMedia = options.PlayMedia;
        _penBrush = new SolidColorBrush(ParseColor(options.PenColor));
        _laser.Fill = _penBrush;
        _controller = new PresentationController(_playlist, scheduler, options.StartIndex, ignoreSteps: _withoutAnimation);

        _front = NewCanvas();
        _back = NewCanvas();
        _back.IsVisible = false;

        _stage = new Panel
        {
            Background = Brushes.Black,
            Children = { _back, _front, _ink, new Canvas { IsHitTestVisible = false, ZIndex = 10, Children = { _laser } } },
        };
        _front.ZIndex = 1;
        _ink.ZIndex = 10;

        if (_rehearse)
        {
            var overlay = BuildRehearsalOverlay();
            overlay.ZIndex = 20;
            _stage.Children.Add(overlay);
        }

        Title = "PulseOffice — Deck";
        Background = Brushes.Black;
        WindowState = WindowState.FullScreen;
        Content = _stage;

        ShowSlide(_front, _controller.SlideIndex);
        PlayEntry();

        if (_rehearse) StartRehearsal();

        Focusable = true;
        KeyDown += OnKeyDown;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += (_, _) => _stroke = null;
        Opened += (_, _) => { PlaceOnMonitor(); Focus(); };
    }

    private void PlaceOnMonitor()
    {
        if (_monitorIndex < 0) return;
        if (Screens.All is not { Count: > 0 } all) return;
        if (_monitorIndex >= all.Count) return;

        var bounds = all[_monitorIndex].Bounds;
        WindowState = WindowState.Normal;
        Position = bounds.Position;
        Width = bounds.Width;
        Height = bounds.Height;
        WindowState = WindowState.FullScreen;
    }

    private static Color ParseColor(string? hex) =>
        !string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex, out var color) ? color : Color.FromRgb(225, 29, 46);

    private Control BuildRehearsalOverlay()
    {
        _timerLabel = new TextBlock
        {
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(236, 238, 244)),
        };

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(180, 20, 20, 26)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 8),
            Margin = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = _timerLabel,
        };
    }

    private void StartRehearsal()
    {
        _totalClock.Restart();
        MarkSlide();
        _timerTick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timerTick.Tick += (_, _) => UpdateTimerLabel();
        _timerTick.Start();
        UpdateTimerLabel();
    }

    private void MarkSlide()
    {
        var id = _controller.Current?.Id;
        if (id == _rehearsalId) return;

        if (_rehearsalId is { } previous) _timeline.Record(previous, (int)_slideClock.ElapsedMilliseconds);
        _rehearsalId = id;
        _slideClock.Restart();
    }

    private void UpdateTimerLabel()
    {
        if (_timerLabel is null) return;
        _timerLabel.Text = $"{Clock(_slideClock.Elapsed)}   /   {Clock(_totalClock.Elapsed)}";
    }

    private static string Clock(TimeSpan span) => $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";

    private readonly ShowSlideSetup _setup = new();

    private SlideView NewCanvas()
    {
        var canvas = new SlideView(_renderer)
        {
            Deck = _deck,
            Size = _deck.Size,
            DeckTheme = _deck.Theme,
            Master = _deck.Master,
            Padding = 0,
            ShowsPrompts = false,
            DesignMode = _forms is null,
            Forms = _forms,
        };
        canvas.ControlPressed += (_, pressed) =>
        {
            if (canvas.Slide is not { } slide || pressed.Element.Content is not PulseOffice.Document.Forms.FormControlObject form) return;
            _controlChanged?.Invoke(slide, pressed.Element with { Content = form with { Properties = pressed.Changed } });
            canvas.InvalidateVisual();
        };
        return canvas;
    }

    private void ShowSlide(SlideView canvas, int index)
    {
        var slide = index >= 0 && index < _playlist.Count ? _playlist[index] : null;
        _setup.Show(canvas, _deck, slide);
    }

    private void FreezeFront()
    {
        if (_inkSlideIndex != _controller.SlideIndex)
        {
            _ink.Children.Clear();
            _inkSlideIndex = _controller.SlideIndex;
        }

        if (_withoutAnimation)
        {
            _front.PlaybackVisual = null;
            _front.InvalidateVisual();
        }
        else
        {
            var frozen = _controller.FrozenTimeMs();
            var schedule = ScheduleFor(_controller.Current);
            _front.PlaybackVisual = frozen < 0 && !HasEntrance(schedule)
                ? null
                : id => _scheduler.VisualAt(id, schedule, frozen);
            _front.InvalidateVisual();
        }

        if (_rehearse) MarkSlide();
        RestartAutoAdvance();
        SlideChanged?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<ScheduledEffect> ScheduleFor(Slide? slide) =>
        slide is null ? [] : _scheduler.Schedule(slide.Animations);

    private bool HasEntrance(IReadOnlyList<ScheduledEffect> schedule) =>
        schedule.Any(s => s.Effect.Category == AnimationCategory.Entrance);

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                if (_tool != PointerTool.None) SetTool(PointerTool.None);
                else Close();
                break;
            case Key.P:
                SetTool(_tool == PointerTool.Pen ? PointerTool.None : PointerTool.Pen);
                break;
            case Key.L:
                SetTool(_tool == PointerTool.Laser ? PointerTool.None : PointerTool.Laser);
                break;
            case Key.E:
                _ink.Children.Clear();
                break;
            case Key.Right or Key.Down or Key.Space or Key.Enter or Key.PageDown:
                Advance();
                break;
            case Key.Left or Key.Up or Key.PageUp or Key.Back:
                Back();
                break;
            case Key.Home:
                _controller.GoTo(0);
                _controller.Back();
                Jump();
                break;
            case Key.End:
                _controller.GoTo(_playlist.Count - 1);
                Jump();
                break;
        }

        e.Handled = true;
    }

    private void SetTool(PointerTool tool)
    {
        _tool = tool;
        _laser.IsVisible = false;
        _stroke = null;
        Cursor = tool == PointerTool.None
            ? Cursor.Default
            : new Cursor(StandardCursorType.Cross);
    }

    public void AdvanceExternal() => Advance();

    public void BackExternal() => Back();

    public int SlidePosition => _controller.SlideIndex;

    public int SlideCount => _playlist.Count;

    public void FirstExternal()
    {
        _controller.GoTo(0);
        _controller.Back();
        Jump();
    }

    public void LastExternal()
    {
        _controller.GoTo(_playlist.Count - 1);
        Jump();
    }

    private void Advance()
    {
        if (_busy)
        {
            _finishAnimation?.Invoke();
            return;
        }

        var slideBefore = _controller.SlideIndex;
        var result = _controller.Advance();

        switch (result)
        {
            case AdvanceResult.RevealedStep:
                PlayStep();
                break;
            case AdvanceResult.ChangedSlide:
                PlayTransition(slideBefore, _controller.SlideIndex);
                break;
            case AdvanceResult.Finished:
                if (_loop && _playlist.Count > 0)
                {
                    var last = _controller.SlideIndex;
                    _controller.GoTo(0);
                    _controller.Back();
                    if (last == 0) Jump();
                    else PlayTransition(last, 0);
                }
                else Close();
                break;
        }
    }

    private void Back()
    {
        if (_busy)
        {
            _finishAnimation?.Invoke();
            return;
        }

        var result = _controller.Back();
        if (result != AdvanceResult.None) Jump();
    }

    private void OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (_tool == PointerTool.Pen)
        {
            var at = e.GetPosition(_stage);
            _stroke = new Polyline
            {
                Stroke = _penBrush,
                StrokeThickness = 3,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                Points = new Points { at },
            };
            _ink.Children.Add(_stroke);
            e.Handled = true;
            return;
        }

        if (_tool == PointerTool.Laser)
        {
            e.Handled = true;
            return;
        }

        if (_busy) return;

        var point = e.GetPosition(_front);
        var element = _front.ElementAt(point);

        if (element is ObjectElement { Content: PulseOffice.Drawing.Assets.RasterImageObject { Map.IsEmpty: false } image }
            && MapTarget(image, _front.BoundsOf(element), point) is { } mapped && Follow(mapped))
        {
            e.Handled = true;
            return;
        }

        if (element is MediaElement media && _playMedia is { } play)
        {
            _ = play(media);
            e.Handled = true;
            return;
        }

        if (element is not null && _controller.Current?.Interaction(element.Id) is { } action && Follow(action))
        {
            e.Handled = true;
            return;
        }

        Advance();
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_tool == PointerTool.Laser)
        {
            var at = e.GetPosition(_stage);
            Canvas.SetLeft(_laser, at.X - _laser.Width / 2);
            Canvas.SetTop(_laser, at.Y - _laser.Height / 2);
            _laser.IsVisible = true;
            return;
        }

        if (_tool == PointerTool.Pen && _stroke is { } stroke
            && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            stroke.Points.Add(e.GetPosition(_stage));
    }

    private InteractiveAction? MapTarget(PulseOffice.Drawing.Assets.RasterImageObject image, Rect bounds, Point point)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return null;
        if (image.Map.At((point.X - bounds.X) / bounds.Width, (point.Y - bounds.Y) / bounds.Height) is not { Target.Length: > 0 } area) return null;

        if (!area.Target.StartsWith('#'))
            return new InteractiveAction { TargetId = image.Id, Kind = InteractionKind.Hyperlink, Reference = area.Target };

        var name = area.Target[1..];
        var slide = _playlist.FirstOrDefault(candidate => candidate.Id == name || candidate.Name == name);
        return slide is null ? null : new InteractiveAction { TargetId = image.Id, Kind = InteractionKind.GoToSlide, Reference = slide.Id };
    }

    private bool Follow(InteractiveAction action)
    {
        var current = _controller.SlideIndex;
        switch (action.Kind)
        {
            case InteractionKind.NextSlide: return GoToSlideIndex(current + 1);
            case InteractionKind.PreviousSlide: return GoToSlideIndex(current - 1);
            case InteractionKind.FirstSlide: return GoToSlideIndex(0);
            case InteractionKind.LastSlide: return GoToSlideIndex(_playlist.Count - 1);
            case InteractionKind.GoToSlide:
                var index = _playlist.ToList().FindIndex(sl => sl.Id == action.Reference);
                return index >= 0 && GoToSlideIndex(index);
            case InteractionKind.StopShow:
                Close();
                return true;
            case InteractionKind.Hyperlink:
                if (action.Reference is { Length: > 1 } local && local[0] == '#')
                {
                    var name = local[1..];
                    var found = _playlist.ToList().FindIndex(sl => sl.Id == name || sl.Name == name);
                    return found >= 0 && GoToSlideIndex(found);
                }
                if (action.Reference is { Length: > 0 } url && Uri.TryCreate(url, UriKind.Absolute, out var uri)
                    && uri.Scheme is "http" or "https" or "mailto")
                    _ = Launcher.LaunchUriAsync(uri);
                return true;
            default:
                return false;
        }
    }

    private bool GoToSlideIndex(int target)
    {
        if (target < 0 || target >= _playlist.Count) return false;
        if (target == _controller.SlideIndex) return true;

        var before = _controller.SlideIndex;
        _controller.GoTo(target);
        PlayTransition(before, target);
        return true;
    }

    private void Jump()
    {
        StopAnimator();
        ResetBack();
        ShowSlide(_front, _controller.SlideIndex);
        FreezeFront();
    }

    private void PlayStep()
    {
        if (_controller.CurrentStepWindow() is not { } window)
        {
            FreezeFront();
            return;
        }

        var schedule = ScheduleFor(_controller.Current);
        var span = Math.Max(1, window.EndMs - window.StartMs);

        AnimateOver(span, progress =>
        {
            var t = window.StartMs + (int)(progress * span);
            _front.PlaybackVisual = id => _scheduler.VisualAt(id, schedule, t);
            _front.InvalidateVisual();
        }, FreezeFront);
    }

    private void PlayTransition(int fromIndex, int toIndex)
    {
        var transition = _playlist[toIndex].Transition;
        if (transition.IsNone)
        {
            ShowSlide(_front, toIndex);
            PlayEntry();
            return;
        }

        ShowSlide(_back, toIndex);
        _back.PlaybackVisual = InitialVisualFor(toIndex);
        _back.IsVisible = true;

        var effect = _transitions.EffectFor(transition);
        var dur = Math.Max(120, transition.DurationMs);

        AnimateOver(dur, progress =>
        {
            var frame = effect.At(progress);
            _transitions.Apply(_back, frame.Incoming, Bounds.Size);
            _transitions.Apply(_front, frame.Outgoing, Bounds.Size);
            _back.ZIndex = frame.OutgoingOnTop ? 0 : 1;
            _front.ZIndex = frame.OutgoingOnTop ? 1 : 0;
        },
        () =>
        {
            ShowSlide(_front, toIndex);
            ResetBack();
            PlayEntry();
        });
    }

    private void ResetBack()
    {
        _back.IsVisible = false;
        _transitions.Reset(_back);
        _transitions.Reset(_front);
        _back.ZIndex = 0;
        _front.ZIndex = 1;
        _back.PlaybackVisual = null;
    }

    private void PlayEntry()
    {
        if (_withoutAnimation || _controller.EntryWindow() is not { } window)
        {
            FreezeFront();
            return;
        }

        var schedule = ScheduleFor(_controller.Current);
        var span = Math.Max(1, window.EndMs - window.StartMs);
        _front.PlaybackVisual = id => _scheduler.VisualAt(id, schedule, window.StartMs);
        _front.InvalidateVisual();

        AnimateOver(span, progress =>
        {
            var t = window.StartMs + (int)(progress * span);
            _front.PlaybackVisual = id => _scheduler.VisualAt(id, schedule, t);
            _front.InvalidateVisual();
        }, FreezeFront);
    }

    private Func<string, ElementVisual>? InitialVisualFor(int index)
    {
        var schedule = ScheduleFor(_playlist[index]);
        return HasEntrance(schedule) ? id => _scheduler.VisualAt(id, schedule, -1) : null;
    }

    private void AnimateOver(int durationMs, Action<double> onFrame, Action onDone)
    {
        StopAnimator();
        _busy = true;

        var clock = System.Diagnostics.Stopwatch.StartNew();
        _finishAnimation = () =>
        {
            onFrame(1.0);
            StopAnimator();
            onDone();
        };
        _animator = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animator.Tick += (_, _) =>
        {
            var progress = Math.Min(1.0, clock.ElapsedMilliseconds / (double)durationMs);
            onFrame(progress);

            if (progress >= 1.0)
            {
                StopAnimator();
                onDone();
            }
        };
        _animator.Start();
    }

    private void StopAnimator()
    {
        _animator?.Stop();
        _animator = null;
        _finishAnimation = null;
        _busy = false;
    }

    private void RestartAutoAdvance()
    {
        _autoAdvance?.Stop();
        _autoAdvance = null;

        if (_rehearse || _manualAdvance) return;

        if (_controller.StepIndex >= _controller.CurrentStepCount() - 1
            && _controller.Current?.Transition is { Advance: AdvanceMode.AfterTime } t
            && t.AdvanceAfterMs > 0)
        {
            _autoAdvance = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(t.AdvanceAfterMs) };
            _autoAdvance.Tick += (_, _) =>
            {
                if (_busy) return;
                _autoAdvance?.Stop();
                Advance();
            };
            _autoAdvance.Start();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        StopAnimator();
        _autoAdvance?.Stop();

        if (_rehearse)
        {
            _timerTick?.Stop();
            if (_rehearsalId is { } id) _timeline.Record(id, (int)_slideClock.ElapsedMilliseconds);
            RehearsalFinished?.Invoke(this, _timeline.Times);
        }

        base.OnClosed(e);
    }
}
