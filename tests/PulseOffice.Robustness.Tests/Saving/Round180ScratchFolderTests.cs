using PulseOffice.Platform;
using Xunit;

namespace PulseOffice.Robustness.Tests.Saving;

public sealed class Round180ScratchFolderTests
{
    [Fact]
    public async Task A_non_local_destination_is_produced_in_an_owner_only_folder_removed_afterwards()
    {
        if (OperatingSystem.IsWindows()) return;
        using var sink = new MemoryStream();
        var file = new ChosenFile("content://remote/doc.ods", "doc.ods")
        {
            OpenRead = _ => Task.FromResult<Stream>(new MemoryStream()),
            OpenWrite = _ => Task.FromResult<Stream>(new NonClosing(sink)),
        };
        string? folder = null;
        UnixFileMode mode = default;

        await new AtomicFileWriter().WriteThroughPathAsync(file, async (path, token) =>
        {
            folder = Path.GetDirectoryName(path)!;
            if (!OperatingSystem.IsWindows()) mode = File.GetUnixFileMode(folder);
            await File.WriteAllTextAsync(path, "body", token);
            return true;
        }, kept => kept, TestContext.Current.CancellationToken);

        Assert.Equal(UnixFileMode.None, mode & (UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute));
        Assert.False(Directory.Exists(folder));
        Assert.Equal("body"u8.ToArray(), sink.ToArray());
    }

    private sealed class NonClosing(Stream inner) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    }
}
