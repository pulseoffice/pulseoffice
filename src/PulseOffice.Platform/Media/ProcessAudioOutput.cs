using System.Diagnostics;
using System.Globalization;

namespace PulseOffice.Platform.Media;

public sealed record AudioPlayerCommand(string Executable, Func<PcmFormat, IReadOnlyList<string>> Arguments, TimeSpan Latency);

public sealed class ProcessAudioOutputFactory(IExecutableLocator executables) : IAudioOutputFactory
{
    private const int PlayerLatencyMs = 100;
    private static readonly TimeSpan PlayerLatency = TimeSpan.FromMilliseconds(PlayerLatencyMs);

    private readonly IReadOnlyList<AudioPlayerCommand> _players =
    [
        new("paplay", format => ["--raw", "--format=s16le", $"--rate={Text(format.SampleRate)}", $"--channels={Text(format.Channels)}", $"--latency-msec={Text(PlayerLatencyMs)}"], PlayerLatency),
        new("pw-cat", format => ["--playback", "--raw", "--format", "s16", "--rate", Text(format.SampleRate), "--channels", Text(format.Channels), "--latency", $"{Text(PlayerLatencyMs)}ms", "-"], PlayerLatency),
        new("aplay", format => ["-q", "-t", "raw", "-f", "S16_LE", "-r", Text(format.SampleRate), "-c", Text(format.Channels), $"--buffer-time={Text(PlayerLatencyMs * 1000)}", "-"], PlayerLatency),
        new("ffplay", format => ["-v", "quiet", "-nodisp", "-autoexit", "-f", "s16le", "-ar", Text(format.SampleRate), "-ch_layout", format.Channels == 1 ? "mono" : "stereo", "-i", "pipe:0"], TimeSpan.FromMilliseconds(250)),
    ];

    public bool IsAvailable => Player() is not null;

    public IAudioOutput? Open(PcmFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (Player() is not { } player) return null;
        var info = new ProcessStartInfo(player.Executable)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in player.Arguments(format)) info.ArgumentList.Add(argument);
        var process = Process.Start(info);
        return process is null ? null : new ProcessAudioOutput(process, new AudioPlayedEstimate(format, player.Latency));
    }

    private AudioPlayerCommand? Player() => _players.FirstOrDefault(player => executables.Exists(player.Executable));

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}

public sealed class AudioPlayedEstimate(PcmFormat format, TimeSpan playerLatency)
{
    public const int PipeCapacityBytes = 65_536;

    private long _written;

    public TimeSpan Played
    {
        get
        {
            var queued = Interlocked.Read(ref _written) - PipeCapacityBytes;
            var played = TimeSpan.FromSeconds((double)Math.Max(0, queued) / format.BytesPerSecond) - playerLatency;
            return played > TimeSpan.Zero ? played : TimeSpan.Zero;
        }
    }

    public void Wrote(int bytes) => Interlocked.Add(ref _written, bytes);
}

public sealed class ProcessAudioOutput(Process process, AudioPlayedEstimate estimate) : IAudioOutput
{
    public TimeSpan Played => estimate.Played;

    public void Write(ReadOnlySpan<byte> pcm)
    {
        if (process.HasExited) throw new IOException("The audio player has exited.");
        process.StandardInput.BaseStream.Write(pcm);
        estimate.Wrote(pcm.Length);
    }

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
