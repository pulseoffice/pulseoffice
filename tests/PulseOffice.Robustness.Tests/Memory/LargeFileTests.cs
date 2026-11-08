using PulseOffice.Formats.Intake;
using PulseOffice.Robustness.Tests.Fuzzing;
using Xunit;

namespace PulseOffice.Robustness.Tests.Memory;

public sealed class LargeFileTests : IDisposable
{
    private sealed class EndlessStream : Stream
    {
        public long Served { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => Served; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Served += count;
            return count;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private readonly DocumentReaders _readers = new();

    public void Dispose() => _readers.Dispose();

    [Fact]
    public async Task A_stream_larger_than_the_limit_is_refused_without_being_held_in_memory()
    {
        var limits = new IncomingDocumentLimits { MaximumFileBytes = 8L << 20 };
        var endless = new EndlessStream();

        var result = await new DocumentBuffer(limits).ReadAsync(endless, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("FMT015", result.Diagnostics[0].Code);
        Assert.True(endless.Served < 9L << 20);
    }

    [Fact]
    public async Task A_seekable_file_larger_than_the_limit_is_refused_before_reading()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pulse-large-{Guid.NewGuid():N}.csv");
        try
        {
            await using (var file = File.Create(path)) file.SetLength(3L << 30);
            await using var stream = File.OpenRead(path);

            var result = await new DocumentBuffer(new IncomingDocumentLimits()).ReadAsync(stream, TestContext.Current.CancellationToken);

            Assert.Equal("FMT015", result.Diagnostics[0].Code);
            Assert.Equal(0, stream.Position);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Opening_and_closing_documents_repeatedly_does_not_grow_memory()
    {
        var seeds = new SeedCorpus().Load(perExtension: 2).Where(seed => seed.Content.Length > 4096).ToList();

        void Cycle()
        {
            foreach (var seed in seeds)
                _readers.Read(seed.Family, "cycle" + Path.GetExtension(seed.Name), new MemoryStream(seed.Content, writable: false));
        }

        for (var warm = 0; warm < 3; warm++) Cycle();
        var before = Settled();
        for (var round = 0; round < 25; round++) Cycle();
        var after = Settled();

        Assert.True(after - before < 24L << 20, $"memory grew by {(after - before) >> 20} MB over 25 rounds of {seeds.Count} documents");
    }

    private long Settled()
    {
        for (var pass = 0; pass < 3; pass++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }

        return GC.GetTotalMemory(forceFullCollection: true);
    }
}
