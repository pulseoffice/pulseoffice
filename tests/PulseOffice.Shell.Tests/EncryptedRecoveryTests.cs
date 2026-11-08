using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Document.Commands;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Packaging.Encryption;
using PulseOffice.Foundation.Modules;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using PulseOffice.UI.Controls.Prompts;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class EncryptedRecoveryTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-recovery-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class SheetProvider : IDocumentSnapshotProvider
    {
        public ModuleKind Module => ModuleKind.Sheets;

        public IRecoveryCapture Capture(OpenDocument document) => new DelegateCapture(document.Model, WriteModelAsync);

        private Task WriteModelAsync(object? model, Stream destination, CancellationToken ct)
        {
            new OdsSpreadsheetWriter().Write((Workbook)model!, destination);
            return Task.CompletedTask;
        }

        public Task<OpenDocument?> RestoreAsync(Stream source, string title, CancellationToken ct = default)
        {
            var result = new OdsSpreadsheetReader().Read(source);
            return Task.FromResult<OpenDocument?>(result.Succeeded
                ? new OpenDocument(ModuleKind.Sheets, title, new Avalonia.Controls.Border(), new UndoStack()) { Model = result.Value }
                : null);
        }
    }

    private sealed class FixedPrompt(string? answer) : IDocumentPasswordPrompt
    {
        public int Asked { get; private set; }

        public Task<string?> AskAsync(string documentName, bool retry, CancellationToken ct)
        {
            Asked++;
            return Task.FromResult(retry ? null : answer);
        }

        public Task<SecretKeyAnswer?> AskSecretKeyAsync(string documentName, OpenPgpKeyProblem problem, CancellationToken ct) =>
            Task.FromResult<SecretKeyAnswer?>(null);

        public Task<ProtectionRequest?> AskProtectionAsync(string documentName, OpenPgpKeyProblem problem, CancellationToken ct) =>
            Task.FromResult<ProtectionRequest?>(null);
    }

    private DocumentRecovery Build(IDocumentPasswordPrompt prompt) =>
        new([new SheetProvider()], NullLogger<DocumentRecovery>.Instance, _directory,
            new DocumentPasswordGate(new OdfPackageEncryption(), prompt, new PulseOffice.Formats.Packaging.Encryption.OpenPgp.BouncyCastleOpenPgp(), new PulseOffice.Formats.Packaging.Encryption.OpenPgp.OpenPgpProtectionFactory(new PulseOffice.Formats.Packaging.Encryption.OpenPgp.BouncyCastleOpenPgp())));

    private static OpenDocument Protected()
    {
        var workbook = new Workbook();
        workbook.AddSheet("Conti").SetValue(new CellAddress(0, 0), CellValue.Text("riservato"));
        return new OpenDocument(ModuleKind.Sheets, "Conti", new Avalonia.Controls.Border(), new UndoStack())
        {
            Model = workbook,
            Protection = new PasswordProtection("Segreto"),
        };
    }

    [Fact]
    public async Task LaCopiaDiUnDocumentoCifratoESalvataCifrata()
    {
        await Build(new FixedPrompt("Segreto")).SnapshotAsync(Protected(), TestContext.Current.CancellationToken);

        var data = Assert.Single(Directory.GetFiles(_directory), file => !file.EndsWith(".json", StringComparison.Ordinal));
        using var stream = new MemoryStream(File.ReadAllBytes(data));
        Assert.True(new OdfPackageEncryption().IsEncrypted(stream));
        Assert.Equal(-1, File.ReadAllBytes(data).AsSpan().IndexOf("riservato"u8));
    }

    [Fact]
    public async Task IlRipristinoChiedeLaPasswordELaConserva()
    {
        await Build(new FixedPrompt("Segreto")).SnapshotAsync(Protected(), TestContext.Current.CancellationToken);

        var prompt = new FixedPrompt("Segreto");
        var recovery = Build(prompt);
        var restored = await recovery.RestoreAsync(recovery.Pending[0], TestContext.Current.CancellationToken);

        Assert.Equal(1, prompt.Asked);
        Assert.NotNull(restored);
        Assert.Equal(new PasswordProtection("Segreto"), restored.Protection);
        Assert.Equal(CellValue.Text("riservato"), ((Workbook)restored.Model!).Sheets[0].GetValue(new CellAddress(0, 0)));
    }

    [Fact]
    public async Task SenzaPasswordIlRipristinoSiFerma()
    {
        await Build(new FixedPrompt("Segreto")).SnapshotAsync(Protected(), TestContext.Current.CancellationToken);

        var recovery = Build(new FixedPrompt(null));

        Assert.Null(await recovery.RestoreAsync(recovery.Pending[0], TestContext.Current.CancellationToken));
    }
}
