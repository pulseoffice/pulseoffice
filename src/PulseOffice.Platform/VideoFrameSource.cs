namespace PulseOffice.Platform;

public sealed record VideoFrame(byte[] Bgra, int DurationMs);

public interface IVideoFrameSource
{
    int Count { get; }

    int Width { get; }

    int Height { get; }

    ValueTask<VideoFrame> RenderAsync(int index, CancellationToken ct);
}

public sealed class VideoFrameException(string code, string message, Exception? inner = null) : Exception(message, inner)
{
    public string Code { get; } = code;
}

public sealed class ListVideoFrameSource(IReadOnlyList<VideoFrame> frames, int width, int height) : IVideoFrameSource
{
    public int Count => frames.Count;

    public int Width => width;

    public int Height => height;

    public ValueTask<VideoFrame> RenderAsync(int index, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(frames[index]);
    }
}
