using PulseOffice.Platform;
using PulseOffice.Platform.Mac;
using PulseOffice.Platform.Windows;

namespace PulseOffice.Desktop.Composition;

public sealed class VideoEncoderProvider(IExecutableLocator executables, IHostSystem host) : IVideoEncoderProvider
{
    private readonly Lazy<IVideoEncoder> _encoder = new(() => Select(executables, host));

    public IVideoEncoder Current => _encoder.Value;

    private static IVideoEncoder Select(IExecutableLocator executables, IHostSystem host)
    {
        if (host.IsMacOS)
            return new VideoToolboxVideoEncoder();

        if (host.IsWindows)
            return new MediaFoundationVideoEncoder();

        var ffmpeg = new FfmpegVideoEncoder(executables);
        return ffmpeg.IsAvailable ? ffmpeg : new UnavailableVideoEncoder("ffmpeg");
    }
}
