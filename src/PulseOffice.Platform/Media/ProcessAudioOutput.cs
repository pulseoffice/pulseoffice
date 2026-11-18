using System.Diagnostics;
using System.Globalization;

namespace PulseOffice.Platform.Media;

public sealed record AudioPlayerCommand(string Executable, Func<PcmFormat, IReadOnlyList<string>> Arguments);

public sealed class ProcessAudioOutputFactory(IExecutableLocator executables) : IAudioOutputFactory
{
    private readonly IReadOnlyList<AudioPlayerCommand> _players =
    [
        new("paplay", format => ["--raw", "--format=s16le", $"--rate={Text(format.SampleRate)}", $"--channels={Text(format.Channels)}"]),
        new("pw-cat", format => ["--playback", "--raw", "--format", "s16", "--rate", Text(format.SampleRate), "--channels", Text(format.Channels), "-"]),
        new("aplay", format => ["-q", "-t", "raw", "-f", "S16_LE", "-r", Text(format.SampleRate), "-c", Text(format.Channels), "-"]),
        new("ffplay", format => ["-v", "quiet", "-nodisp", "-autoexit", "-f", "s16le", "-ar", Text(format.SampleRate), "-ch_layout", format.Channels == 1 ? "mono" : "stereo", "-i", "pipe:0"]),
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
        return process is null ? null : new ProcessAudioOutput(process);
    }

    private AudioPlayerCommand? Player() => _players.FirstOrDefault(player => executables.Exists(player.Executable));

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}

public sealed class ProcessAudioOutput(Process process) : IAudioOutput
{
    public void Write(ReadOnlySpan<byte> pcm)
    {
        if (process.HasExited) throw new IOException("The audio player has exited.");
        process.StandardInput.BaseStream.Write(pcm);
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
