using PulseOffice.Platform;

namespace PulseOffice.Shell;

public enum DocumentCopyOutcome { Failed, Written, WrittenWithoutPassword }

public interface IDocumentCopyWriter
{
    DocumentCopyOutcome Write(OpenDocument document, string path);
}

public sealed class DocumentCopyWriter(
    IDocumentOperationsCatalog operations,
    IDocumentPasswordGate passwords,
    IAtomicFileWriter files) : IDocumentCopyWriter
{
    public DocumentCopyOutcome Write(OpenDocument document, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (operations.For(document) is not { } module) return DocumentCopyOutcome.Failed;

        using var plain = new MemoryStream();
        if (!module.WriteTo(document, path, plain)) return DocumentCopyOutcome.Failed;

        if (document.Protection is not { } protection)
            return Commit(path, plain) ? DocumentCopyOutcome.Written : DocumentCopyOutcome.Failed;

        plain.Position = 0;
        if (!passwords.CanProtect(plain))
            return Commit(path, plain) ? DocumentCopyOutcome.WrittenWithoutPassword : DocumentCopyOutcome.Failed;

        using var encrypted = passwords.Protect(plain, protection);
        return Commit(path, encrypted) ? DocumentCopyOutcome.Written : DocumentCopyOutcome.Failed;
    }

    private bool Commit(string path, MemoryStream content) =>
        files.Replace(path, destination =>
        {
            content.Position = 0;
            content.CopyTo(destination);
            return true;
        }, written => written);
}
