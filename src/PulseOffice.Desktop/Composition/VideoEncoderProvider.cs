using PulseOffice.Platform;
using PulseOffice.Platform.Mac;
using PulseOffice.Platform.Windows;

namespace PulseOffice.Desktop.Composition;

public sealed class VideoEncoderProvider(IExecutableLocator executables, IExternalProcess processes, IHostSystem host) : IVideoEncoderProvider
{
    private readonly Lazy<IVideoEncoder> _encoder = new(() => Select(executables, processes, host));

    public IVideoEncoder Current => _encoder.Value;

    private static IVideoEncoder Select(IExecutableLocator executables, IExternalProcess processes, IHostSystem host)
    {
        if (host.IsMacOS)
            return new VideoToolboxVideoEncoder();

        if (host.IsWindows)
            return new MediaFoundationVideoEncoder();

        var ffmpeg = new FfmpegVideoEncoder(executables, new FfmpegEncoderChoice(processes));
        return ffmpeg.IsAvailable ? ffmpeg : new UnavailableVideoEncoder("ffmpeg");
    }
}
