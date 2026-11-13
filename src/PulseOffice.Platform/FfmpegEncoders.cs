namespace PulseOffice.Platform;

public sealed record FfmpegEncoder(string Name, IReadOnlyList<string> Options);

public interface IFfmpegEncoderChoice
{
    Task<FfmpegEncoder?> ChooseAsync(CancellationToken ct = default);
}

public sealed class FfmpegEncoderChoice(IExternalProcess processes) : IFfmpegEncoderChoice
{
    private static readonly IReadOnlyList<FfmpegEncoder> Preferred =
    [
        new("libx264", ["-pix_fmt", "yuv420p"]),
        new("libopenh264", ["-pix_fmt", "yuv420p", "-b:v", "6M"]),
        new("mpeg4", ["-pix_fmt", "yuv420p", "-q:v", "2"]),
    ];

    private readonly SemaphoreSlim _gate = new(1, 1);
    private FfmpegEncoder? _chosen;
    private bool _asked;

    public async Task<FfmpegEncoder?> ChooseAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_asked) return _chosen;
            var listing = await processes.RunAsync("ffmpeg", ["-hide_banner", "-encoders"], ct: ct).ConfigureAwait(false);
            if (listing is not { Succeeded: true }) return null;
            var offered = Offered(listing.Output);
            _chosen = Preferred.FirstOrDefault(encoder => offered.Contains(encoder.Name));
            _asked = true;
            return _chosen;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static HashSet<string> Offered(string listing) =>
        listing.Split('\n')
            .Select(line => line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(fields => fields.Length >= 2 && fields[0].Length == 6 && fields[0][0] == 'V')
            .Select(fields => fields[1])
            .ToHashSet(StringComparer.Ordinal);
}
