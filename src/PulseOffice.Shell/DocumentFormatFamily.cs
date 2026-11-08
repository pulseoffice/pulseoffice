namespace PulseOffice.Shell;

public interface IDocumentFormatFamily
{
    string Of(string? path);
}

public sealed class DocumentFormatFamily : IDocumentFormatFamily
{
    private const string OpenDocument = "ODF";
    private const string OfficeOpenXml = "OOXML";

    private readonly HashSet<string> _openDocument = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ods", ".odt", ".odp", ".ots", ".ott", ".otp", ".fods", ".fodt", ".fodp",
    };

    private readonly HashSet<string> _officeOpenXml = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xlsm", ".xltx", ".xltm", ".docx", ".docm", ".dotx", ".dotm", ".pptx", ".pptm", ".potx", ".potm",
    };

    public string Of(string? path)
    {
        if (string.IsNullOrEmpty(path)) return OpenDocument;
        var extension = Path.GetExtension(path);
        if (_openDocument.Contains(extension)) return OpenDocument;
        if (_officeOpenXml.Contains(extension)) return OfficeOpenXml;
        return extension.Length > 1 ? extension[1..].ToUpperInvariant() : OpenDocument;
    }
}
