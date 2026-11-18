using Microsoft.Extensions.Logging;
using PulseOffice.Platform;
using PulseOffice.Platform.Media;

namespace PulseOffice.Desktop.Composition;

public sealed class MediaPlaybackProvider(
    IExecutableLocator executables,
    IExternalProcess processes,
    IHostSystem host,
    ITemporaryFiles temporary,
    ILogger<DecodedMediaPlayback> logger) : IMediaPlaybackFactory
{
    private readonly Lazy<IMediaPlaybackFactory> _chosen = new(() => Select(executables, processes, host, temporary, logger));

    public bool IsAvailable => _chosen.Value.IsAvailable;

    public IMediaPlayback Create() => _chosen.Value.Create();

    private static IMediaPlaybackFactory Select(
        IExecutableLocator executables, IExternalProcess processes, IHostSystem host, ITemporaryFiles temporary, ILogger<DecodedMediaPlayback> logger)
    {
        var ffmpeg = new FfmpegMediaDecoder(executables, processes);
        IMediaDecoder decoder;
        IAudioOutputFactory audio;
        if (host.IsMacOS)
        {
            var native = new AvFoundationMediaDecoder();
            decoder = native.IsAvailable ? native : ffmpeg;
            audio = new AudioQueueOutputFactory();
        }
        else if (host.IsWindows)
        {
            decoder = new MediaFoundationMediaDecoder();
            audio = new WaveOutAudioOutputFactory();
        }
        else
        {
            decoder = ffmpeg;
            audio = new ProcessAudioOutputFactory(executables);
        }

        return decoder.IsAvailable
            ? new DecodedMediaPlaybackFactory(decoder, audio, temporary, logger)
            : new UnavailableMediaPlaybackFactory();
    }
}
