using Avalonia;
using Avalonia.Controls;
using PulseOffice.Localization;
using PulseOffice.Platform.Media;
using PulseOffice.Presentations;
using PulseOffice.UI.Controls.Objects;

namespace PulseOffice.Presentations.UI;

public sealed record ShowMediaOptions(
    Func<MediaElement, Task<MediaSession?>> Open,
    Func<MediaElement, Task> Fallback,
    ITextCatalog Texts);

public sealed class ShowMediaStage
{
    private readonly ShowMediaOptions? _options;
    private readonly Func<SlideElement, Rect> _bounds;
    private readonly Dictionary<string, (MediaElement Element, MediaView View)> _playing = new(StringComparer.Ordinal);
    private readonly MediaAutoPlay _autoPlay = new();
    private int _generation;

    public ShowMediaStage(ShowMediaOptions? options, Func<SlideElement, Rect> bounds)
    {
        ArgumentNullException.ThrowIfNull(bounds);
        _options = options;
        _bounds = bounds;
        Layer = new Canvas { ZIndex = 4 };
    }

    public Canvas Layer { get; }

    public bool Handles(SlideElement? element) => element is MediaElement && _options is not null;

    public Task ActivateAsync(MediaElement media) => StartAsync(media, fallback: true);

    public void StartAutomatic(Slide? slide)
    {
        if (slide is null || _options is null) return;
        foreach (var media in _autoPlay.Starting(slide))
            if (!_playing.ContainsKey(media.Id)) _ = StartAsync(media, fallback: false);
    }

    private async Task StartAsync(MediaElement media, bool fallback)
    {
        ArgumentNullException.ThrowIfNull(media);
        if (_options is not { } options) return;
        if (_playing.TryGetValue(media.Id, out var current))
        {
            current.View.Session.Toggle();
            return;
        }

        var generation = _generation;
        var session = await options.Open(media).ConfigureAwait(true);
        if (session is null)
        {
            if (fallback) await options.Fallback(media).ConfigureAwait(true);
            return;
        }

        if (generation != _generation || _playing.ContainsKey(media.Id))
        {
            session.Dispose();
            return;
        }

        var view = new MediaView(session, options.Texts, media.Kind == MediaKind.Video && session.Playback.Info is { HasVideo: true });
        _playing[media.Id] = (media, view);
        Layer.Children.Add(view);
        Place(media, view);
        session.Start();
    }

    public void Relayout()
    {
        foreach (var (element, view) in _playing.Values) Place(element, view);
    }

    public void Clear()
    {
        _generation++;
        foreach (var (_, view) in _playing.Values) view.Dispose();
        _playing.Clear();
        Layer.Children.Clear();
    }

    private void Place(MediaElement element, MediaView view)
    {
        var bounds = _bounds(element);
        Canvas.SetLeft(view, bounds.X);
        Canvas.SetTop(view, bounds.Y);
        view.Width = Math.Max(1, bounds.Width);
        view.Height = Math.Max(1, bounds.Height);
    }
}
