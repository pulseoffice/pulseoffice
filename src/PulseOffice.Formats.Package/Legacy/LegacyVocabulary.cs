using System.Xml.Linq;

namespace PulseOffice.Formats.Packaging.Legacy;

public sealed class LegacyVocabulary
{
    public static readonly XNamespace LegacyOffice = "http://openoffice.org/2000/office";
    public static readonly XNamespace LegacyManifest = "http://openoffice.org/2001/manifest";
    public const string LegacyMimePrefix = "application/vnd.sun.xml.";

    private readonly Dictionary<XNamespace, XNamespace> _namespaces = new()
    {
        [LegacyOffice] = OdfPackage.Namespaces.Office,
        ["http://openoffice.org/2000/style"] = OdfPackage.Namespaces.Style,
        ["http://openoffice.org/2000/text"] = OdfPackage.Namespaces.Text,
        ["http://openoffice.org/2000/table"] = OdfPackage.Namespaces.Table,
        ["http://openoffice.org/2000/drawing"] = OdfPackage.Namespaces.Draw,
        ["http://www.w3.org/1999/XSL/Format"] = OdfPackage.Namespaces.Fo,
        ["http://openoffice.org/2000/datastyle"] = OdfPackage.Namespaces.Number,
        ["http://openoffice.org/2000/presentation"] = OdfPackage.Namespaces.Presentation,
        ["http://www.w3.org/2000/svg"] = OdfPackage.Namespaces.Svg,
        ["http://openoffice.org/2000/chart"] = OdfPackage.Namespaces.Chart,
        ["http://openoffice.org/2000/dr3d"] = "urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0",
        ["http://openoffice.org/2000/form"] = OdfPackage.Namespaces.Form,
        ["http://openoffice.org/2000/script"] = OdfPackage.Namespaces.Script,
        ["http://openoffice.org/2000/meta"] = OdfPackage.Namespaces.Meta,
        ["http://openoffice.org/2001/config"] = "urn:oasis:names:tc:opendocument:xmlns:config:1.0",
        [LegacyManifest] = OdfPackage.Namespaces.Manifest,
    };

    private readonly Dictionary<string, string> _prefixes = new(StringComparer.Ordinal)
    {
        ["office"] = OdfPackage.Namespaces.Office.NamespaceName,
        ["style"] = OdfPackage.Namespaces.Style.NamespaceName,
        ["text"] = OdfPackage.Namespaces.Text.NamespaceName,
        ["table"] = OdfPackage.Namespaces.Table.NamespaceName,
        ["draw"] = OdfPackage.Namespaces.Draw.NamespaceName,
        ["fo"] = OdfPackage.Namespaces.Fo.NamespaceName,
        ["number"] = OdfPackage.Namespaces.Number.NamespaceName,
        ["presentation"] = OdfPackage.Namespaces.Presentation.NamespaceName,
        ["svg"] = OdfPackage.Namespaces.Svg.NamespaceName,
        ["chart"] = OdfPackage.Namespaces.Chart.NamespaceName,
        ["form"] = OdfPackage.Namespaces.Form.NamespaceName,
        ["script"] = OdfPackage.Namespaces.Script.NamespaceName,
        ["meta"] = OdfPackage.Namespaces.Meta.NamespaceName,
        ["manifest"] = OdfPackage.Namespaces.Manifest.NamespaceName,
        ["xlink"] = OdfPackage.Namespaces.Xlink.NamespaceName,
        ["dc"] = OdfPackage.Namespaces.Dc.NamespaceName,
        ["config"] = "urn:oasis:names:tc:opendocument:xmlns:config:1.0",
        ["dr3d"] = "urn:oasis:names:tc:opendocument:xmlns:dr3d:1.0",
        ["ooo"] = OdfPackage.Namespaces.Ooo.NamespaceName,
    };

    private readonly Dictionary<string, string> _mimeTypes = new(StringComparer.Ordinal)
    {
        ["application/vnd.sun.xml.calc"] = OdfPackage.MimeTypes.Spreadsheet,
        ["application/vnd.sun.xml.calc.template"] = OdfPackage.MimeTypes.SpreadsheetTemplate,
        ["application/vnd.sun.xml.impress"] = OdfPackage.MimeTypes.Presentation,
        ["application/vnd.sun.xml.impress.template"] = OdfPackage.MimeTypes.PresentationTemplate,
        ["application/vnd.sun.xml.writer"] = OdfPackage.MimeTypes.Text,
        ["application/vnd.sun.xml.draw"] = OdfPackage.MimeTypes.Graphics,
        ["application/vnd.sun.xml.chart"] = OdfPackage.MimeTypes.Chart,
        ["application/vnd.sun.xml.math"] = "application/vnd.oasis.opendocument.formula",
    };

    private readonly Dictionary<string, string> _bodyClasses = new(StringComparer.Ordinal)
    {
        ["spreadsheet"] = "spreadsheet",
        ["presentation"] = "presentation",
        ["drawing"] = "drawing",
        ["chart"] = "chart",
        ["text"] = "text",
    };

    public XNamespace Map(XNamespace ns) => _namespaces.GetValueOrDefault(ns, ns);

    public bool IsLegacy(XNamespace ns) => _namespaces.ContainsKey(ns);

    public IReadOnlyDictionary<string, string> Prefixes => _prefixes;

    public string MapMimeType(string mimeType) => _mimeTypes.GetValueOrDefault(mimeType.Trim(), mimeType);

    public string BodyFor(string? documentClass) =>
        documentClass is not null && _bodyClasses.TryGetValue(documentClass, out var body) ? body : "spreadsheet";
}
