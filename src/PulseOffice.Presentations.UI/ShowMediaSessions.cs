using PulseOffice.Platform.Media;
using PulseOffice.Presentations;

namespace PulseOffice.Presentations.UI;

public interface IShowMediaSessions
{
    Task<MediaSession?> OpenAsync(MediaElement media, string? documentPath);

    Task<MediaSession?> OpenSoundAsync(EmbeddedSound sound, bool loop);
}

public sealed class ShowMediaSessions(IDeckMediaFiles mediaFiles, IMediaPlaybackFactory playback, MediaVolumeCurve curve) : IShowMediaSessions
{
    private readonly MediaFileExtensions _extensions = new();

    public async Task<MediaSession?> OpenAsync(MediaElement media, string? documentPath)
    {
        ArgumentNullException.ThrowIfNull(media);
        if (!playback.IsAvailable || Source(media, documentPath) is not { } source) return null;
        var player = playback.Create();
        try
        {
            if (await player.OpenAsync(source).ConfigureAwait(true)) return new MediaSession(player, Settings(media), curve);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
        }

        player.Dispose();
        return null;
    }

    public async Task<MediaSession?> OpenSoundAsync(EmbeddedSound sound, bool loop)
    {
        ArgumentNullException.ThrowIfNull(sound);
        if (!playback.IsAvailable || sound.Data.Length == 0) return null;
        var player = playback.Create();
        try
        {
            var extension = Path.GetExtension(sound.Path) is { Length: > 1 } named ? named : _extensions.FromType(sound.MimeType);
            if (await player.OpenAsync(new StreamMediaSource(new MemoryStream(sound.Data, writable: false), extension)).ConfigureAwait(true))
                return new MediaSession(player, new MediaPlaybackSettings { Loop = loop }, curve);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
            or System.ComponentModel.Win32Exception)
        {
        }

        player.Dispose();
        return null;
    }

    public MediaPlaybackSettings Settings(MediaElement media)
    {
        ArgumentNullException.ThrowIfNull(media);
        return new MediaPlaybackSettings
        {
            Loop = media.Loop,
            Volume = media.Volume,
            Muted = media.Muted,
            TrimStart = media.TrimStart,
            TrimEnd = media.TrimEnd,
            FadeIn = media.FadeIn,
            FadeOut = media.FadeOut,
        };
    }

    private MediaSource? Source(MediaElement media, string? documentPath) => mediaFiles.Source(media, documentPath) switch
    {
        LinkedMediaFile { Exists: true } file => new FileMediaSource(file.Path),
        EmbeddedMediaSource => new StreamMediaSource(new MemoryStream(media.Data, writable: false), _extensions.For(media)),
        _ => null,
    };
}
