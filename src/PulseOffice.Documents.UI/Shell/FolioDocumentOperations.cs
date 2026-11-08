using PulseOffice.Formats;
using PulseOffice.Foundation.Modules;
using PulseOffice.Printing;
using PulseOffice.Shell;

namespace PulseOffice.Documents.UI.Shell;

public sealed class FolioDocumentOperations(
    PulseOffice.Documents.UI.Rendering.IFolioPrintables printables,
    PulseOffice.Documents.UI.Rendering.IFolioViewMarks marks,
    ITextDocumentFormatRegistry formats,
    PulseOffice.Documents.UI.Editing.IFolioImages images,
    PulseOffice.Documents.Layout.Flow.IPrintLayouts paginated,
    PulseOffice.Platform.IAtomicFileWriter atomic,
    PulseOffice.Documents.Blocks.IBlockTree tree,
    PulseOffice.Document.Versions.IVersionSequenceComparer comparer,
    PulseOffice.Localization.ITextCatalog texts) : IDocumentOperations
{
    public PulseOffice.Document.Versions.VersionComparison? CompareVersion(OpenDocument document, string path)
    {
        if (document?.Model is not TextDocument current) return null;
        using var stream = File.OpenRead(path);
        if (formats.Read(path, stream).Value is not { } saved) return null;
        return comparer.Compare(Items(saved), Items(current));
    }

    private IReadOnlyList<PulseOffice.Document.Versions.VersionItem> Items(TextDocument text) =>
        [.. tree.Paragraphs(text.Body).Select((paragraph, index) =>
            new PulseOffice.Document.Versions.VersionItem(texts.Format("versions.compare.paragraph", index + 1), paragraph.Text))];

    public bool InsertImage(OpenDocument document, byte[] data, string name) =>
        document?.View is FolioView view && images.Insert(view.Editor, data, name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : "image/png", name);

    public ModuleKind Module => ModuleKind.Documents;

    public string SuggestedName(OpenDocument document) => Path.GetFileNameWithoutExtension(document.Title);

    public IPrintableDocument? BuildPrintable(OpenDocument document) => BuildPrintable(document, marks.PrintComments);

    public IPrintableDocument? BuildPrintable(OpenDocument document, bool comments) =>
        document?.View is FolioView view ? printables.From(paginated.Of(view.Editor.Document, view.Editor.Layout), view.Editor.Document, comments) : null;

    public IPrintableDocument? PreviewFile(string path)
    {
        using var stream = File.OpenRead(path);
        return formats.Read(path, stream).Value is { } text ? printables.From(paginated.Layout(text), text, printComments: false) : null;
    }

    public bool SupportsPrintComments(OpenDocument document) => document.Model is TextDocument { Comments.All.Count: > 0 };

    public bool PrintCommentsByDefault => marks.PrintComments;

    public PulseOffice.Document.Metadata.DocumentProperties? Properties(OpenDocument document) => (document.Model as TextDocument)?.Properties;

    public string NativeExtension => "odt";

    public string OfficeExtension => "docx";

    public bool WriteTo(OpenDocument document, string path)
    {
        if (document.Model is not TextDocument text || formats.WriterFor(path) is not { } writer) return false;
        return atomic.Replace(path, stream => writer.Write(text, stream).Succeeded, written => written);
    }

    public string? RenderHtml(OpenDocument document)
    {
        if (document?.Model is not TextDocument text || formats.WriterFor("page.html") is not { } writer) return null;
        using var buffer = new MemoryStream();
        return writer.Write(text, buffer).Succeeded ? System.Text.Encoding.UTF8.GetString(buffer.ToArray()) : null;
    }

    public PulseOffice.Document.Versions.DocumentVersionCollection? Versions(OpenDocument document) => (document.Model as TextDocument)?.Versions;

    public string TemplateExtension => "ott";

    public string? OfficeTemplateExtension => "dotx";

}
