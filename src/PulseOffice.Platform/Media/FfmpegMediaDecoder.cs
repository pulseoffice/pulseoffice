using System.Diagnostics;
using System.Globalization;

namespace PulseOffice.Platform.Media;

public sealed class FfmpegMediaDecoder(IExecutableLocator executables, IExternalProcess processes) : IMediaDecoder
{
    private const int FramesPerSecond = 25;
    private const int MaxWidth = 1280;

    public bool IsAvailable => executables.Exists("ffmpeg") && executables.Exists("ffprobe");

    public async Task<MediaInfo?> ProbeAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var outcome = await processes.RunAsync("ffprobe",
            ["-v", "error", "-show_entries", "format=duration:stream=codec_type,width,height", "-of", "default=noprint_wrappers=1", path],
            ct: ct).ConfigureAwait(false);
        return outcome is { Succeeded: true } ? Parse(outcome.Output) : null;
    }

    public IMediaAudioStream? OpenAudio(string path, TimeSpan start, PcmFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        var process = Start(path, start,
            ["-vn", "-sn", "-ac", Text(format.Channels), "-ar", Text(format.SampleRate), "-f", "s16le", "pipe:1"]);
        return process is null ? null : new PipedAudio(process);
    }

    public IMediaVideoStream? OpenVideo(string path, MediaInfo info, TimeSpan start)
    {
        ArgumentNullException.ThrowIfNull(info);
        var (width, height) = Fit(info.Width, info.Height);
        var process = Start(path, start,
            ["-an", "-sn", "-vf", $"scale={width}:{height}", "-r", Text(FramesPerSecond), "-pix_fmt", "bgra", "-f", "rawvideo", "pipe:1"]);
        return process is null ? null : new PipedVideo(process, width, height, start, FramesPerSecond);
    }

    private static Process? Start(string path, TimeSpan start, IReadOnlyList<string> output)
    {
        var info = new ProcessStartInfo("ffmpeg")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in new[] { "-v", "quiet", "-nostdin", "-ss", start.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture), "-i", path })
            info.ArgumentList.Add(argument);
        foreach (var argument in output) info.ArgumentList.Add(argument);
        return Process.Start(info);
    }

    private static (int Width, int Height) Fit(int width, int height)
    {
        if (width <= 0 || height <= 0) return (640, 360);
        if (width <= MaxWidth) return (Even(width), Even(height));
        return (MaxWidth, Even((int)Math.Round(height * (double)MaxWidth / width)));
    }

    private static int Even(int value) => Math.Max(2, value - value % 2);

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static MediaInfo? Parse(string output)
    {
        var duration = TimeSpan.Zero;
        var hasAudio = false;
        var hasVideo = false;
        var width = 0;
        var height = 0;
        foreach (var line in output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0) continue;
            var key = line[..separator];
            var value = line[(separator + 1)..];
            switch (key)
            {
                case "codec_type" when value == "audio":
                    hasAudio = true;
                    break;
                case "codec_type" when value == "video":
                    hasVideo = true;
                    break;
                case "width" when width == 0 && int.TryParse(value, CultureInfo.InvariantCulture, out var w):
                    width = w;
                    break;
                case "height" when height == 0 && int.TryParse(value, CultureInfo.InvariantCulture, out var h):
                    height = h;
                    break;
                case "duration" when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds):
                    duration = TimeSpan.FromSeconds(seconds);
                    break;
            }
        }

        hasVideo &= width > 0 && height > 0;
        return duration > TimeSpan.Zero ? new MediaInfo(duration, hasAudio, hasVideo, width, height) : null;
    }

    private abstract class PipedStream(Process process) : IDisposable
    {
        protected Stream Output => process.StandardOutput.BaseStream;

        public void Dispose()
        {
            try
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            process.Dispose();
        }
    }

    private sealed class PipedAudio(Process process) : PipedStream(process), IMediaAudioStream
    {
        public int Read(Span<byte> pcm) => Output.ReadAtLeast(pcm, Math.Min(2, pcm.Length), throwOnEndOfStream: false);
    }

    private sealed class PipedVideo(Process process, int width, int height, TimeSpan start, int fps) : PipedStream(process), IMediaVideoStream
    {
        private long _index;

        public MediaFrame? Next()
        {
            var bgra = new byte[width * height * 4];
            if (Output.ReadAtLeast(bgra, bgra.Length, throwOnEndOfStream: false) < bgra.Length) return null;
            var time = start + TimeSpan.FromSeconds(_index++ / (double)fps);
            return new MediaFrame(width, height, bgra, time);
        }
    }
}
