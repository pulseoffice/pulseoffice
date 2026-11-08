using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using PulseOffice.Formats.Packaging;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.Odf.TextCodecs;

internal interface IOdfFieldElements
{
    XName? NameOf(TextFieldKind kind);

    XElement Build(TextFieldKind kind, string shown);

    TextFieldKind KindOf(XName name);
}

internal sealed class OdfFieldElements : OdfXml, IOdfFieldElements
{

    private readonly IReadOnlyList<(TextFieldKind Kind, XName Name)> _table =
    [
        (TextFieldKind.SlideNumber, Text + "page-number"),
        (TextFieldKind.SlideCount, Text + "page-count"),
        (TextFieldKind.SlideTitle, OdfPackage.Namespaces.Loext + "page-name"),
        (TextFieldKind.Date, Text + "date"),
        (TextFieldKind.Time, Text + "time"),
        (TextFieldKind.Author, Text + "author-name"),
        (TextFieldKind.FileName, Text + "file-name"),
    ];

    public XName? NameOf(TextFieldKind kind) => _table.FirstOrDefault(entry => entry.Kind == kind).Name;

    public XElement Build(TextFieldKind kind, string shown) =>
        NameOf(kind) is { } name
            ? new XElement(name, AttributesOf(kind), shown)
            : new XElement(Text + "span", shown);

    private IEnumerable<XAttribute> AttributesOf(TextFieldKind kind) =>
        kind == TextFieldKind.FileName ? [new XAttribute(Text + "display", "name-and-extension")] : [];

    public TextFieldKind KindOf(XName name) => _table.FirstOrDefault(entry => entry.Name == name).Kind;
}
