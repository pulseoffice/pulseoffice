namespace PulseOffice.Formats.Odf;

public interface IContentSpool
{
    Stream Create();
}

public sealed class MemoryContentSpool : IContentSpool
{
    public Stream Create() => new MemoryStream();
}

public sealed class OverflowContentSpool(Func<Stream> overflow, long threshold) : IContentSpool
{
    public const long DefaultThreshold = 64L * 1024 * 1024;

    public Stream Create() => new OverflowStream(overflow, threshold);

    private sealed class OverflowStream(Func<Stream> overflow, long threshold) : Stream
    {
        private Stream _current = new MemoryStream();

        public bool Overflowed { get; private set; }

        public override bool CanRead => _current.CanRead;

        public override bool CanSeek => _current.CanSeek;

        public override bool CanWrite => _current.CanWrite;

        public override long Length => _current.Length;

        public override long Position
        {
            get => _current.Position;
            set => _current.Position = value;
        }

        public override void Flush() => _current.Flush();

        public override int Read(byte[] buffer, int offset, int count) => _current.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _current.Read(buffer);

        public override long Seek(long offset, SeekOrigin origin) => _current.Seek(offset, origin);

        public override void SetLength(long value)
        {
            Make(value);
            _current.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Make(_current.Position + buffer.Length);
            _current.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            Make(_current.Position + 1);
            _current.WriteByte(value);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _current.Dispose();
            base.Dispose(disposing);
        }

        private void Make(long length)
        {
            if (Overflowed || length <= threshold) return;

            var file = overflow();
            try
            {
                var position = _current.Position;
                _current.Position = 0;
                _current.CopyTo(file);
                file.Position = position;
            }
            catch
            {
                file.Dispose();
                throw;
            }

            _current.Dispose();
            _current = file;
            Overflowed = true;
        }
    }
}
