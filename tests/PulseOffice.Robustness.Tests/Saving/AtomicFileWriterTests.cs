using PulseOffice.Platform;
using Xunit;

namespace PulseOffice.Robustness.Tests.Saving;

public sealed class AtomicFileWriterTests : IDisposable
{
    private sealed class FailingStream(int failAfter) : Stream
    {
        private int _given;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _given; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_given >= failAfter) throw new IOException("No space left on device");
            var chunk = Math.Min(count, failAfter - _given);
            Array.Fill(buffer, (byte)'x', offset, chunk);
            _given += chunk;
            return chunk;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private readonly string _folder = Directory.CreateTempSubdirectory("pulse-atomic-").FullName;
    private readonly AtomicFileWriter _writer = new();

    public void Dispose()
    {
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(_folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void Il_file_temporaneo_si_puo_rileggere_come_chiede_il_pacchetto_OOXML()
    {
        var path = Path.Combine(_folder, "modello.potx");
        var themes = new PulseOffice.Presentations.ThemeCatalog();
        var masters = new PulseOffice.Presentations.MasterFactory();
        var theme = themes.Find("Sand")!;
        var deck = new PulseOffice.Presentations.Presentation { Theme = theme, Master = masters.CreateStandard(theme) };

        var written = _writer.Replace(path,
            stream => new PulseOffice.Formats.OpenXml.PptxWriter(PulseOffice.Formats.OpenXml.PptxFlavour.Template).Write(deck, stream).Succeeded,
            ok => ok);

        Assert.True(written);
        Assert.True(new FileInfo(path).Length > 0);
    }

    [Fact]
    public async Task A_write_that_fails_halfway_leaves_the_original_and_no_leftovers()
    {
        var path = Path.Combine(_folder, "bilancio.ods");
        await File.WriteAllTextAsync(path, "originale", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<IOException>(() => _writer.ReplaceAsync(path, new FailingStream(100_000), TestContext.Current.CancellationToken));

        Assert.Equal("originale", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Single(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task A_folder_without_write_permission_fails_cleanly_and_keeps_the_original()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix permissions");
            return;
        }

        var path = Path.Combine(_folder, "bilancio.ods");
        await File.WriteAllTextAsync(path, "originale", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(_folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);

        var failure = await Record.ExceptionAsync(() => _writer.ReplaceAsync(path, new MemoryStream("nuovo"u8.ToArray()), TestContext.Current.CancellationToken));

        Assert.True(failure is UnauthorizedAccessException or IOException, failure?.GetType().Name);
        Assert.Equal("originale", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Replacing_keeps_the_owner_only_permissions_of_a_private_document()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix permissions");
            return;
        }

        var path = Path.Combine(_folder, "segreto.odt");
        await File.WriteAllTextAsync(path, "originale", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        await _writer.ReplaceAsync(path, new MemoryStream("nuovo"u8.ToArray()), TestContext.Current.CancellationToken);

        Assert.Equal("nuovo", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
    }

    [Fact]
    public async Task Save_as_over_an_existing_local_file_is_atomic_too()
    {
        var path = Path.Combine(_folder, "bilancio.xlsx");
        await File.WriteAllTextAsync(path, "originale", TestContext.Current.CancellationToken);
        var opened = false;
        var chosen = new ChosenFile(path, "bilancio.xlsx")
        {
            OpenRead = _ => Task.FromResult<Stream>(File.OpenRead(path)),
            OpenWrite = _ =>
            {
                opened = true;
                return Task.FromResult<Stream>(File.Create(path));
            },
        };

        await Assert.ThrowsAsync<IOException>(() => _writer.WriteAsync(chosen, new FailingStream(10), TestContext.Current.CancellationToken));

        Assert.False(opened);
        Assert.Equal("originale", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }
    [Fact]
    public void An_export_that_reports_failure_leaves_the_previous_file_untouched()
    {
        var path = Path.Combine(_folder, "relazione.pdf");
        File.WriteAllText(path, "originale");

        var kept = _writer.Replace(path, stream =>
        {
            stream.Write("mezzo"u8);
            return false;
        }, written => written);

        Assert.False(kept);
        Assert.Equal("originale", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(_folder));
    }

    [Fact]
    public async Task An_encoder_that_writes_to_a_path_gets_a_temporary_with_the_same_extension()
    {
        var path = Path.Combine(_folder, "diapositive.mp4");
        File.WriteAllText(path, "originale");
        var chosen = new ChosenFile(path, "diapositive.mp4")
        {
            OpenRead = _ => Task.FromResult<Stream>(File.OpenRead(path)),
            OpenWrite = _ => Task.FromResult<Stream>(File.Create(path)),
        };
        string? used = null;

        var kept = await _writer.WriteThroughPathAsync(chosen, async (temporary, ct) =>
        {
            used = temporary;
            await File.WriteAllTextAsync(temporary, "video", ct);
            return true;
        }, written => written, TestContext.Current.CancellationToken);

        Assert.True(kept);
        Assert.Equal(".mp4", Path.GetExtension(used));
        Assert.NotEqual(path, used);
        Assert.Equal("video", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(_folder));
    }

    [Fact]
    public void A_name_near_the_file_system_limit_still_saves()
    {
        var path = Path.Combine(_folder, new string('n', 240) + ".ods");

        var written = _writer.Replace(path, stream => { stream.WriteByte(1); return true; }, ok => ok);

        Assert.True(written);
        Assert.Equal(1, new FileInfo(path).Length);
    }
}
