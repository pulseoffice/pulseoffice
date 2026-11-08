using Microsoft.Extensions.Logging.Abstractions;
using PulseOffice.Document.Commands;
using PulseOffice.Formats.Packaging.Encryption;
using PulseOffice.Foundation.Modules;
using Xunit;

namespace PulseOffice.Shell.Tests;

public class ShellAuditTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "pulseoffice-audit-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

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

        public Task<OpenDocument?> RestoreAsync(Stream source, string title, CancellationToken ct = default) =>
            Task.FromResult<OpenDocument?>(null);
    }

    [Fact]
    public async Task UnaCopiaInChiaroNonSopravviveQuandoIlDocumentoVieneProtetto()
    {
        var recovery = new DocumentRecovery([new TextProvider()], NullLogger<DocumentRecovery>.Instance, _directory);
        var document = new OpenDocument(ModuleKind.Sheets, "Conti", new Avalonia.Controls.Panel(), new UndoStack()) { Model = "riservato" };
        await recovery.SnapshotAsync(document, TestContext.Current.CancellationToken);
        Assert.NotEmpty(Directory.GetFiles(_directory));

        document.Protection = new PasswordProtection("Segreto");
        await recovery.SnapshotAsync(document, TestContext.Current.CancellationToken);

        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public void LaCapienzaDeiRecentiSegueLImpostazione()
    {
        var store = Path.Combine(_directory, "recenti.json");
        var files = Enumerable.Range(0, 3).Select(index => Path.Combine(_directory, $"f{index}.ods")).ToList();
        Directory.CreateDirectory(_directory);
        foreach (var file in files) File.WriteAllText(file, "x");
        var capacity = 2;
        var recent = new RecentDocuments(NullLogger<RecentDocuments>.Instance, store, () => capacity);

        foreach (var file in files) recent.Remember(file, Path.GetFileName(file));
        Assert.Equal(2, recent.Items.Count);

        capacity = 1;
        Assert.Single(recent.Items);
    }

    [Fact]
    public void ChiudendoLUltimoDocumentoChiAscoltaLaChiusuraNonLoVedePiuAttivo()
    {
        var workspace = new DocumentWorkspace();
        var document = new OpenDocument(ModuleKind.Sheets, "Conti", new Avalonia.Controls.Panel(), new UndoStack());
        workspace.Open(document);
        OpenDocument? activeAtClose = document;
        workspace.DocumentClosed += (_, _) => activeAtClose = workspace.Active;

        workspace.Close(document);

        Assert.Null(activeAtClose);
    }
}
