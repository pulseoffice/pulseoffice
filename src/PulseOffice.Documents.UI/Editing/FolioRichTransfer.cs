using System.Text;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Editing;
using PulseOffice.Exchange;
using PulseOffice.Formats;

namespace PulseOffice.Documents.UI.Editing;

public interface IFolioRichTransfer
{
    IReadOnlyList<ExchangeRepresentation> Export(DocumentFragment fragment, string text);

    DocumentFragment? Import(ExchangePackage package);

    DocumentFragment? ImportHtml(string html);
}

public sealed class FolioRichTransfer(ITextDocumentInterchange interchange, ISubdocuments subdocuments)
    : IFolioRichTransfer
{
    private readonly IReadOnlyList<(string Id, ExchangeFlavor Flavor, string? MediaType)> _exports =
    [
        ("html", ExchangeFlavor.Html, null),
        ("rtf", ExchangeFlavor.Rtf, null),
        ("odt", ExchangeFlavor.OpenDocument, ExchangeMediaTypes.Text),
        ("docx", ExchangeFlavor.OpenXml, ExchangeMediaTypes.WordDocument),
    ];

    public IReadOnlyList<ExchangeRepresentation> Export(DocumentFragment fragment, string text)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        var document = Document(fragment);
        var result = new List<ExchangeRepresentation>();
        if (!string.IsNullOrEmpty(text)) result.Add(ExchangeRepresentation.OfText(ExchangeFlavor.Text, text));
        foreach (var (id, flavor, mediaType) in _exports)
            if (interchange.Write(id, document) is { Length: > 0 } bytes)
                result.Add(new ExchangeRepresentation(flavor, bytes) { MediaType = mediaType });
        return result;
    }

    public DocumentFragment? Import(ExchangePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var document = Read("odt", package.Find(ExchangeFlavor.OpenDocument, ExchangeMediaTypes.Text))
            ?? Read("docx", package.Find(ExchangeFlavor.OpenXml, ExchangeMediaTypes.WordDocument))
            ?? Read("rtf", package.Find(ExchangeFlavor.Rtf))
            ?? Read("html", package.Find(ExchangeFlavor.Html));
        return Whole(document);
    }

    public DocumentFragment? ImportHtml(string html) =>
        Whole(interchange.Read("html", Encoding.UTF8.GetBytes(html ?? string.Empty)));

    private TextDocument? Read(string id, ExchangeRepresentation? representation) =>
        representation is null ? null : interchange.Read(id, representation.Data);

    private DocumentFragment? Whole(TextDocument? document) =>
        document is null || document.Body.Count == 0 ? null : subdocuments.Whole(document);

    private TextDocument Document(DocumentFragment fragment)
    {
        var document = new TextDocument(fragment.Styles);
        document.Body.Replace(fragment.Blocks.Clone().Blocks);
        document.Notes.Replace(fragment.Notes);
        document.Comments.Replace(fragment.Comments);
        document.Objects.Replace(fragment.Objects);
        document.Changes.Replace(fragment.Changes);
        return document;
    }
}
