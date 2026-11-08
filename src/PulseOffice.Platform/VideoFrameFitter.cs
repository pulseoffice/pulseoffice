namespace PulseOffice.Platform;

public sealed class VideoFrameFitter
{
    public IVideoFrameSource? Fit(IVideoFrameSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Width < 2 || source.Height < 2) return null;

        return new FittedVideoFrameSource(source);
    }

    private sealed class FittedVideoFrameSource(IVideoFrameSource inner) : IVideoFrameSource
    {
        private const int BytesPerPixel = 4;

        public int Count => inner.Count;

        public int Width { get; } = inner.Width & ~1;

        public int Height { get; } = inner.Height & ~1;

        public async ValueTask<VideoFrame> RenderAsync(int index, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var frame = await inner.RenderAsync(index, ct).ConfigureAwait(false);

            if (frame.Bgra is null || frame.Bgra.Length != (long)inner.Width * inner.Height * BytesPerPixel)
                throw new VideoFrameException("VIDEO004", "Frames do not match the declared size.");

            return Width == inner.Width && Height == inner.Height
                ? frame
                : frame with { Bgra = Crop(frame.Bgra) };
        }

        private byte[] Crop(byte[] source)
        {
            var row = Width * BytesPerPixel;
            var cropped = new byte[row * Height];

            for (var y = 0; y < Height; y++)
                Buffer.BlockCopy(source, y * inner.Width * BytesPerPixel, cropped, y * row, row);

            return cropped;
        }
    }
}
