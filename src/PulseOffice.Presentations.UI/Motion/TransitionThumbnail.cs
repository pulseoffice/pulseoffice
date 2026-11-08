using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Theming;
using PulseOffice.UI.Theming;

namespace PulseOffice.Presentations.UI.Motion;

public interface ITransitionSamples
{
    Control Outgoing(Size size);

    Control Incoming(Size size);
}

public sealed class StylizedTransitionSamples : ITransitionSamples
{
    private readonly IBrush _outgoingPaper = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB));
    private readonly IBrush _outgoingInk = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
    private readonly IBrush _incomingPaper = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
    private readonly IBrush _incomingInk = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));

    public Control Outgoing(Size size) => Card(size, _outgoingPaper, _outgoingInk, 0.55);

    public Control Incoming(Size size) => Card(size, _incomingPaper, _incomingInk, 0.75);

    private Control Card(Size size, IBrush background, IBrush ink, double titleShare)
    {
        var lines = new StackPanel
        {
            Spacing = Math.Max(2, size.Height * 0.08),
            Margin = new Thickness(size.Width * 0.12, size.Height * 0.2, size.Width * 0.12, 0),
        };
        lines.Children.Add(Bar(size.Width * 0.76 * titleShare, Math.Max(3, size.Height * 0.12), ink));
        lines.Children.Add(Bar(size.Width * 0.76, Math.Max(2, size.Height * 0.05), ink, 0.55));
        lines.Children.Add(Bar(size.Width * 0.62, Math.Max(2, size.Height * 0.05), ink, 0.55));
        lines.Children.Add(Bar(size.Width * 0.7, Math.Max(2, size.Height * 0.05), ink, 0.55));
        return new Border { Width = size.Width, Height = size.Height, Child = lines, Background = background };
    }

    private Control Bar(double width, double height, IBrush ink, double opacity = 1) =>
        new Border
        {
            Width = Math.Max(4, width),
            Height = height,
            CornerRadius = new CornerRadius(height / 2),
            Opacity = opacity,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = ink,
        };
}

public sealed class SlideTransitionSamples(IDeckSlidePictures pictures, Presentation deck, Slide? previous, Slide current) : ITransitionSamples
{
    private readonly StylizedTransitionSamples _fallback = new();

    public Control Outgoing(Size size) =>
        previous is null ? _fallback.Outgoing(size) : Backed(pictures.Create(deck, previous, size.Width, size.Height, new SlidePictureOptions { Framed = false }), size);

    public Control Incoming(Size size) => Backed(pictures.Create(deck, current, size.Width, size.Height, new SlidePictureOptions { Framed = false }), size);

    private Control Backed(Control picture, Size size) =>
        new Border { Width = size.Width, Height = size.Height, Background = Brushes.White, Child = picture };
}

public sealed class TransitionThumbnail : UserControl
{
    private const double RestingProgress = 0.45;
    private const int MinimumPlayMs = 500;
    private const int MaximumPlayMs = 1400;
    private const int HoldMs = 450;

    private readonly TransitionStage _stage;
    private readonly Panel _layers = new() { ClipToBounds = true };
    private readonly Control _outgoing;
    private readonly Control _incoming;
    private readonly Size _size;
    private ITransitionEffect? _effect;
    private SlideTransition _transition;
    private DispatcherTimer? _timer;
    private DateTime _started;

    public TransitionThumbnail(TransitionStage stage, SlideTransition transition, ITransitionSamples samples, Size size, bool playOnHover = true)
    {
        _stage = stage ?? throw new ArgumentNullException(nameof(stage));
        ArgumentNullException.ThrowIfNull(samples);
        _transition = transition ?? throw new ArgumentNullException(nameof(transition));
        _size = size;
        _effect = transition.IsNone ? null : stage.EffectFor(transition);
        _outgoing = samples.Outgoing(size);
        _incoming = samples.Incoming(size);
        _layers.Width = size.Width;
        _layers.Height = size.Height;
        _layers.Children.Add(_outgoing);
        _layers.Children.Add(_incoming);
        Content = _layers;
        ShowAt(RestProgress);
        if (playOnHover)
        {
            PointerEntered += (_, _) => Play();
            PointerExited += (_, _) => Stop();
        }
        DetachedFromVisualTree += (_, _) => Stop();
    }

    public SlideTransition Transition => _transition;

    public bool IsPlaying => _timer is not null;

    public double Progress { get; private set; }

    public Control OutgoingLayer => _outgoing;

    public Control IncomingLayer => _incoming;

    private double RestProgress => _transition.IsNone ? 1 : RestingProgress;

    public void Show(SlideTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (transition == _transition) return;
        _transition = transition;
        _effect = transition.IsNone ? null : _stage.EffectFor(transition);
        if (!IsPlaying) ShowAt(RestProgress);
    }

    public void ShowAt(double progress)
    {
        Progress = Math.Clamp(progress, 0, 1);
        if (_effect is null)
        {
            _stage.Reset(_outgoing);
            _stage.Reset(_incoming);
            _outgoing.IsVisible = Progress < 0.5;
            _incoming.IsVisible = Progress >= 0.5;
            return;
        }

        _outgoing.IsVisible = true;
        _incoming.IsVisible = true;
        var frame = _effect.At(Progress);
        _stage.Apply(_outgoing, frame.Outgoing, _size);
        _stage.Apply(_incoming, frame.Incoming, _size);
        _outgoing.ZIndex = frame.OutgoingOnTop ? 1 : 0;
        _incoming.ZIndex = frame.OutgoingOnTop ? 0 : 1;
    }

    public void Play()
    {
        Stop();
        _started = DateTime.UtcNow;
        ShowAt(0);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Advance();
        _timer.Start();
    }

    public void Stop()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer = null;
        ShowAt(RestProgress);
    }

    private int PlayMs => Math.Clamp(_transition.DurationMs, MinimumPlayMs, MaximumPlayMs);

    private void Advance()
    {
        var elapsed = (DateTime.UtcNow - _started).TotalMilliseconds;
        if (elapsed >= PlayMs + HoldMs)
        {
            _started = DateTime.UtcNow;
            ShowAt(0);
            return;
        }
        ShowAt(elapsed / PlayMs);
    }
}
