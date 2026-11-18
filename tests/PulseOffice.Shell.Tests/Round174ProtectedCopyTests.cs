using Avalonia.Controls;
using PulseOffice.Document.Commands;
using PulseOffice.Formats.Packaging.Encryption;
using PulseOffice.Foundation.Modules;
using PulseOffice.Platform;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round174ProtectedCopyTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("pulseoffice-copy-174-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed class Operations : IDocumentOperations
    {
        public ModuleKind Module => ModuleKind.Sheets;
        public string NativeExtension => "ods";
        public string OfficeExtension => "xlsx";
        public string TemplateExtension => "ots";
        public string? OfficeTemplateExtension => null;
        public string SuggestedName(OpenDocument document) => "Budget";
        public IPrintableDocument? BuildPrintable(OpenDocument document) => null;
        public PulseOffice.Document.Metadata.DocumentProperties? Properties(OpenDocument document) => null;
        public bool WriteTo(OpenDocument document, string path) => false;

        public bool WriteTo(OpenDocument document, string path, Stream destination)
        {
            destination.Write("plain"u8);
            return true;
        }

        public string? RenderHtml(OpenDocument document) => null;
        public PulseOffice.Document.Versions.DocumentVersionCollection? Versions(OpenDocument document) => null;
    }

    private sealed class Gate(bool odf) : IDocumentPasswordGate
    {
        public Task<(UnlockOutcome Outcome, UnlockedDocument? Document)> UnlockAsync(MemoryStream buffer, string documentName, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ProtectionChoice?> ChooseProtectionAsync(string documentName, CancellationToken ct) => throw new NotSupportedException();

        public bool CanProtect(MemoryStream package) => odf;

        public bool IsProtected(string path) => false;

        public MemoryStream Protect(MemoryStream package, OdfProtection protection) => new("sealed"u8.ToArray());

        public Task<bool> CommitAsync(MemoryStream package, OdfProtection? protection, Func<MemoryStream, Task> commit) =>
            throw new NotSupportedException();
    }

    private (DocumentCopyOutcome Outcome, string Content) Copy(bool odf)
    {
        var document = new OpenDocument(ModuleKind.Sheets, "Budget", new Panel(), new UndoStack())
        {
            Protection = new PasswordProtection("secret"),
        };
        var path = Path.Combine(_folder, "copy.ods");
        var outcome = new DocumentCopyWriter(new DocumentOperationsCatalog([new Operations()]), new Gate(odf), new AtomicFileWriter())
            .Write(document, path);
        return (outcome, File.ReadAllText(path));
    }

    [Fact]
    public void A_copy_of_a_protected_document_is_encrypted_with_its_password()
    {
        var (outcome, content) = Copy(odf: true);
        Assert.Equal(DocumentCopyOutcome.Written, outcome);
        Assert.Equal("sealed", content);
    }

    [Fact]
    public void A_copy_in_a_format_without_encryption_says_it_lost_the_password()
    {
        var (outcome, content) = Copy(odf: false);
        Assert.Equal(DocumentCopyOutcome.WrittenWithoutPassword, outcome);
        Assert.Equal("plain", content);
    }
}
