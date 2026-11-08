using PulseOffice.Foundation.Modules;
using PulseOffice.Printing;

namespace PulseOffice.Shell;

public interface IDocumentOperations
{
    ModuleKind Module { get; }

    string SuggestedName(OpenDocument document);

    IPrintableDocument? BuildPrintable(OpenDocument document);

    IPrintableDocument? BuildPrintable(OpenDocument document, bool comments) => BuildPrintable(document);

    IPrintableDocument? BuildExportable(OpenDocument document, bool comments) => BuildPrintable(document, comments);

    IPrintableDocument? PreviewFile(string path) => null;

    IPrintableDocument? BuildPages(OpenDocument document) => BuildPrintable(document);

    IPrintableDocument? BuildRange(OpenDocument document, string reference) => null;

    PdfExportProfile PdfExport(OpenDocument document) => new(HasHeadings: true, LayoutLabel: string.Empty, Layouts: []);

    bool SupportsPrintComments(OpenDocument document) => false;

    bool PrintCommentsByDefault => false;

    PulseOffice.Document.Metadata.DocumentProperties? Properties(OpenDocument document);

    bool InsertImage(OpenDocument document, byte[] data, string name) => false;

    PulseOffice.Security.IMacroSignatureHolder? MacroSigning(OpenDocument document) =>
        document.Model as PulseOffice.Security.IMacroSignatureHolder;

    string NativeExtension { get; }

    string OfficeExtension { get; }

    bool WriteTo(OpenDocument document, string path);

    string? RenderHtml(OpenDocument document);

    PulseOffice.Document.Versions.DocumentVersionCollection? Versions(OpenDocument document);

    PulseOffice.Document.Versions.VersionComparison? CompareVersion(OpenDocument document, string path) => null;

    string TemplateExtension { get; }

    string? OfficeTemplateExtension { get; }

}

public sealed record PdfExportLayout(string Label, Func<IPrintableDocument> Build);

public sealed record PdfExportProfile(bool HasHeadings, string LayoutLabel, IReadOnlyList<PdfExportLayout> Layouts);

public interface IDocumentOperationsCatalog
{
    IReadOnlyList<IDocumentOperations> All { get; }

    IDocumentOperations? For(OpenDocument? document);
}

public sealed class DocumentOperationsCatalog(IEnumerable<IDocumentOperations> operations) : IDocumentOperationsCatalog
{
    private readonly IReadOnlyList<IDocumentOperations> _operations = [.. operations];

    public IReadOnlyList<IDocumentOperations> All => _operations;

    public IDocumentOperations? For(OpenDocument? document) =>
        document is null ? null : _operations.FirstOrDefault(candidate => candidate.Module == document.Module);
}

public interface IPrintPreferences
{
    string? PrinterId { get; set; }

    int Copies { get; set; }

    bool Duplex { get; set; }

    bool Collate { get; set; }
}

public sealed class PrintPreferences : IPrintPreferences
{
    public string? PrinterId { get; set; }

    public int Copies { get; set; } = 1;

    public bool Duplex { get; set; }

    public bool Collate { get; set; } = true;
}
