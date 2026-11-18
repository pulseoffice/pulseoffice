using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using PulseOffice.Localization;
using PulseOffice.Platform.Media;

namespace PulseOffice.UI.Controls.Objects;

public sealed class MediaView : UserControl, IDisposable
{
    private const string PlayGlyph = "▶";
    private const string PauseGlyph = "❚❚";

    private readonly MediaSession _session;
    private readonly ITextCatalog _texts;
    private readonly Lock _gate = new();
    private readonly Image _picture = new() { Stretch = Stretch.Uniform };
    private readonly Button _toggle = new() { MinWidth = 36, Padding = new Thickness(6, 2), Focusable = false };
    private readonly Slider _seek = new() { Minimum = 0, Maximum = 1, VerticalAlignment = VerticalAlignment.Center, Focusable = false };
    private readonly Border _bar;
    private WriteableBitmap? _bitmap;
    private MediaFrame? _pending;
    private bool _scheduled;
    private bool _moving;
    private bool _disposed;

    public MediaView(MediaSession session, ITextCatalog texts, bool showsPicture)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(texts);
        _session = session;
        _texts = texts;

        _toggle.Click += (_, _) =>
        {
            _session.Toggle();
            RefreshToggle();
        };
        _seek.AddHandler(PointerPressedEvent, (_, _) => _moving = true, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _seek.AddHandler(PointerReleasedEvent, (_, _) =>
        {
            _moving = false;
            _session.SeekTo(_seek.Value);
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        var controls = new DockPanel { LastChildFill = true, Margin = new Thickness(6, 4) };
        DockPanel.SetDock(_toggle, Dock.Left);
        controls.Children.Add(_toggle);
        controls.Children.Add(_seek);
        _seek.Margin = new Thickness(8, 0, 0, 0);

        _bar = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xB0, 0x10, 0x10, 0x10)),
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = controls,
            Opacity = showsPicture ? 0 : 1,
        };

        Background = showsPicture ? Brushes.Black : Brushes.Transparent;
        Content = new Panel { Children = { _picture, _bar } };
        _picture.IsVisible = showsPicture;

        PointerEntered += (_, _) => _bar.Opacity = 1;
        PointerExited += (_, _) => _bar.Opacity = showsPicture ? 0 : 1;

        _session.Playback.FrameReady += OnFrame;
        _session.Progress += OnProgress;
        _session.Finished += OnFinished;
        RefreshToggle();
    }

    public MediaSession Session => _session;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Playback.FrameReady -= OnFrame;
        _session.Progress -= OnProgress;
        _session.Finished -= OnFinished;
        _session.Dispose();
        _picture.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && !_bar.IsPointerOver)
        {
            _session.Toggle();
            RefreshToggle();
        }

        e.Handled = true;
    }

    private void OnFrame(object? sender, MediaFrame frame)
    {
        Interlocked.Exchange(ref _pending, frame);
        lock (_gate)
        {
            if (_scheduled) return;
            _scheduled = true;
        }

        Dispatcher.UIThread.Post(Present, DispatcherPriority.Render);
    }

    private void Present()
    {
        lock (_gate) _scheduled = false;
        if (_disposed || Interlocked.Exchange(ref _pending, null) is not { } frame) return;
        if (_bitmap is null || _bitmap.PixelSize.Width != frame.Width || _bitmap.PixelSize.Height != frame.Height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(new PixelSize(frame.Width, frame.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
        }

        using (var buffer = _bitmap.Lock())
        {
            for (var row = 0; row < frame.Height; row++)
                Marshal.Copy(frame.Bgra, row * frame.Stride, buffer.Address + row * buffer.RowBytes, frame.Stride);
        }

        _picture.Source = null;
        _picture.Source = _bitmap;
        _picture.InvalidateVisual();
    }

    private void OnProgress(object? sender, TimeSpan elapsed) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed) return;
            var length = _session.Window.Length;
            if (!_moving && length > TimeSpan.Zero) _seek.Value = Math.Clamp(elapsed / length, 0, 1);
            RefreshToggle();
        });

    private void OnFinished(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed) RefreshToggle();
        });

    private void RefreshToggle()
    {
        var playing = _session.IsPlaying;
        _toggle.Content = playing ? PauseGlyph : PlayGlyph;
        ToolTip.SetTip(_toggle, _texts[playing ? "media.view.pause" : "media.player.play"]);
    }
}
