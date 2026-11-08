namespace PulseOffice.Formats.Interchange.Text;

public static class TextInterchangeFormats
{
    public static readonly TextDocumentFormat PlainText = new()
    {
        Id = "txt",
        DisplayName = "Text",
        Extensions = [".txt", ".text"],
        MimeType = "text/plain",
    };

    public static readonly TextDocumentFormat Html = new()
    {
        Id = "html",
        DisplayName = "HTML",
        Extensions = [".html", ".htm"],
        MimeType = "text/html",
    };

    public static readonly TextDocumentFormat Xhtml = new()
    {
        Id = "xhtml",
        DisplayName = "XHTML",
        Extensions = [".xhtml"],
        MimeType = "application/xhtml+xml",
    };

    public static readonly TextDocumentFormat Epub = new()
    {
        Id = "epub",
        DisplayName = "EPUB",
        Extensions = [".epub"],
        MimeType = "application/epub+zip",
        ExportOnly = true,
    };

    public static readonly TextDocumentFormat Rtf = new()
    {
        Id = "rtf",
        DisplayName = "Rich Text Format",
        Extensions = [".rtf"],
        MimeType = "application/rtf",
    };
}
