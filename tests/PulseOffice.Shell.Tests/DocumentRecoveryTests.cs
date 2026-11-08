using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Document.Commands;
using PulseOffice.Foundation.Modules;
using PulseOffice.Platform;
using PulseOffice.Shell;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class DocumentRecoveryTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-recovery-" + Guid.NewGuid().ToString("N"));

    private sealed class TextProvider : IDocumentSnapshotProvider
    {
        public ModuleKind Module => ModuleKind.Sheets;

        public IRecoveryCapture Capture(OpenDocument document) => new DelegateCapture(document.Model, WriteModelAsync);

        private async Task WriteModelAsync(object? model, Stream destination, CancellationToken ct)
        {
            var writer = new StreamWriter(destination);
            await writer.WriteAsync(model as string ?? string.Empty);
            await writer.FlushAsync(ct);
        }

        public async Task<OpenDocument?> RestoreAsync(Stream source, string title, CancellationToken ct = default)
        {
            var content = await new StreamReader(source).ReadToEndAsync(ct);
            return Document(title, content);
        }
    }

    private sealed class FailingProvider : IDocumentSnapshotProvider
    {
        public ModuleKind Module => ModuleKind.Sheets;

        public IRecoveryCapture Capture(OpenDocument document) => new DelegateCapture(document.Model, WriteModelAsync);

        private async Task WriteModelAsync(object? model, Stream destination, CancellationToken ct)
        {
            await destination.WriteAsync(new byte[] { 1, 2, 3 }, ct);
            throw new InvalidOperationException("broken");
        }

        public Task<OpenDocument?> RestoreAsync(Stream source, string title, CancellationToken ct = default) =>
            Task.FromResult<OpenDocument?>(null);
    }

    [Fact]
    public void UnManifestoConIdentificativoFuoriCartellaSiIgnora()
    {
        Directory.CreateDirectory(_directory);
        var outside = Path.Combine(Path.GetDirectoryName(_directory)!, Path.GetFileName(_directory) + "-vittima.dati");
        File.WriteAllText(outside, "x");
        try
        {
            var id = "../" + Path.GetFileName(_directory) + "-vittima";
            File.WriteAllText(Path.Combine(_directory, "m.json"),
                $"{{\"Id\":\"{id}\",\"Module\":0,\"Title\":\"T\",\"OriginalPath\":null,\"SavedAt\":\"2026-01-01T00:00:00+00:00\"}}");

            var recovery = new DocumentRecovery([new TextProvider()], NullLogger<DocumentRecovery>.Instance, _directory);
            Assert.Empty(recovery.Pending);
            recovery.Discard(id);
            recovery.DiscardAll();

            Assert.True(File.Exists(outside));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public async Task LeCopieDiRecuperoSonoLeggibiliSoloDalProprietario()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix permissions");
            return;
        }

        var recovery = new DocumentRecovery([new TextProvider()], NullLogger<DocumentRecovery>.Instance, _directory);

        await recovery.SnapshotAsync(Document("Stipendi", "riservato"), Ct);

        var owner = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.Equal(owner | UnixFileMode.UserExecute, File.GetUnixFileMode(_directory));
        Assert.NotEmpty(Directory.GetFiles(_directory));
        foreach (var file in Directory.GetFiles(_directory)) Assert.Equal(owner, File.GetUnixFileMode(file));
    }

    [Fact]
    public async Task Recovery_copies_go_through_the_private_file_store()
    {
        var files = new RecordingPrivateFiles();
        var recovery = new DocumentRecovery([new TextProvider()], NullLogger<DocumentRecovery>.Instance, _directory, privateFiles: files);

        await recovery.SnapshotAsync(Document("Stipendi", "riservato"), Ct);

        Assert.Contains(_directory, files.Folders);
        Assert.NotEmpty(files.Opened);
        Assert.All(files.Opened, path => Assert.StartsWith(_directory, path, StringComparison.Ordinal));
    }

    private sealed class RecordingPrivateFiles : IPrivateFiles
    {
        public List<string> Folders { get; } = [];

        public List<string> Opened { get; } = [];

        public void CreateFolder(string folder)
        {
            Folders.Add(folder);
            Directory.CreateDirectory(folder);
        }

        public FileStream Open(string path, FileMode mode, FileShare share = FileShare.None)
        {
            Opened.Add(path);
            return new FileStream(path, mode, FileAccess.Write, share);
        }

        public void Restrict(string path)
        {
        }
    }

    [Fact]
    public async Task UnaCopiaFallitaNonLasciaFileTemporanei()
    {
        var recovery = new DocumentRecovery([new FailingProvider()], NullLogger<DocumentRecovery>.Instance, _directory);

        await recovery.SnapshotAsync(Document("Conti", "x"), Ct);

        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task IlDocumentoRipristinatoConservaIlPercorsoOriginale()
    {
        var original = Document("Conti", "contenuto");
        original.MarkSaved("/dati/Conti.ods", "Conti.ods");
        original.MarkModified();
        await Build().SnapshotAsync(original, Ct);

        var second = Build();
        var restored = await second.RestoreAsync(second.Pending[0], Ct);

        Assert.NotNull(restored);
        Assert.Equal("/dati/Conti.ods", restored.Path);
        Assert.True(restored.IsModified);
    }

    private static OpenDocument Document(string title, string content) =>
        new(ModuleKind.Sheets, title, new Avalonia.Controls.Panel(), new UndoStack()) { Model = content };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private DocumentRecovery Build() =>
        new([new TextProvider()], NullLogger<DocumentRecovery>.Instance, _directory);

    [Fact]
    public async Task UnaCopiaSiRilegge()
    {
        await Build().SnapshotAsync(Document("Conti", "contenuto"), Ct);

        var second = Build();
        var pending = Assert.Single(second.Pending);
        Assert.Equal("Conti", pending.Title);

        var restored = await second.RestoreAsync(pending, Ct);
        Assert.NotNull(restored);
        Assert.Equal("contenuto", restored.Model);
    }

    [Fact]
    public async Task IlDocumentoRipristinatoRisultaModificato()
    {
        await Build().SnapshotAsync(Document("Conti", "contenuto"), Ct);

        var second = Build();
        var restored = await second.RestoreAsync(second.Pending[0], Ct);

        Assert.True(restored!.IsModified);
    }

    [Fact]
    public async Task DueCopieDelloStessoDocumentoNonSiSommano()
    {
        var recovery = Build();
        var document = Document("Conti", "uno");

        await recovery.SnapshotAsync(document, Ct);
        await recovery.SnapshotAsync(document, Ct);

        Assert.Single(Build().Pending);
    }

    [Fact]
    public async Task ScartareUnDocumentoNeRimuoveLaCopia()
    {
        var recovery = Build();
        var document = Document("Conti", "uno");
        await recovery.SnapshotAsync(document, Ct);

        recovery.DiscardFor(document);

        Assert.Empty(Build().Pending);
    }

    [Fact]
    public async Task UnaCopiaSenzaDatiNonSiPropone()
    {
        await Build().SnapshotAsync(Document("Conti", "uno"), Ct);

        foreach (var file in Directory.GetFiles(_directory, "*.dati")) File.Delete(file);

        Assert.Empty(Build().Pending);
    }

    [Fact]
    public async Task IlDocumentoRipristinatoTieneLaSuaCopiaFinoAlSalvataggio()
    {
        await Build().SnapshotAsync(Document("Conti", "contenuto"), Ct);

        var second = Build();
        var restored = await second.RestoreAsync(second.Pending[0], Ct);

        Assert.NotNull(restored);
        Assert.Empty(second.Pending);
        Assert.Single(Build().Pending);

        await second.SnapshotAsync(restored, Ct);
        Assert.Single(Build().Pending);

        second.DiscardFor(restored);
        Assert.Empty(Build().Pending);
    }

    [Fact]
    public async Task UnDocumentoInSolaLetturaRestaInSolaLetturaDopoIlRecupero()
    {
        var document = Document("Conti", "contenuto");
        document.IsReadOnly = true;
        await Build().SnapshotAsync(document, Ct);

        var second = Build();
        Assert.True(second.Pending[0].ReadOnly);
        var restored = await second.RestoreAsync(second.Pending[0], Ct);

        Assert.True(restored!.IsReadOnly);
    }

    private sealed class GatedProvider : IDocumentSnapshotProvider
    {
        private int _active;

        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MostActive { get; private set; }

        public ModuleKind Module => ModuleKind.Sheets;

        public IRecoveryCapture Capture(OpenDocument document) => new DelegateCapture(document.Model, WriteModelAsync);

        private async Task WriteModelAsync(object? model, Stream destination, CancellationToken ct)
        {
            MostActive = Math.Max(MostActive, Interlocked.Increment(ref _active));
            await Gate.Task.WaitAsync(ct);
            await destination.WriteAsync(new byte[] { 1 }, ct);
            Interlocked.Decrement(ref _active);
        }

        public Task<OpenDocument?> RestoreAsync(Stream source, string title, CancellationToken ct = default) =>
            Task.FromResult<OpenDocument?>(null);
    }

    [Fact]
    public async Task CopieDiRecuperoSovrappostePerLoStessoDocumentoSiScrivonoUnaAllaVolta()
    {
        var provider = new GatedProvider();
        var recovery = new DocumentRecovery([provider], NullLogger<DocumentRecovery>.Instance, _directory);
        var document = Document("Conti", "uno");

        var first = recovery.SnapshotAsync(document, Ct);
        var second = recovery.SnapshotAsync(document, Ct);
        var idle = recovery.WhenIdleAsync();
        await Task.Delay(50, Ct);
        provider.Gate.SetResult();
        await Task.WhenAll(first, second, idle);

        Assert.Equal(1, provider.MostActive);
        Assert.Single(new DocumentRecovery([provider], NullLogger<DocumentRecovery>.Instance, _directory).Pending);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
