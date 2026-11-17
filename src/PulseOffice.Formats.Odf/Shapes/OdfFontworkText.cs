using System.Xml.Linq;
using PulseOffice.Formats.Odf.TextCodecs;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdfFontworkText(IOdfParagraphText paragraphs)
{
    public XElement Paragraph(string text) => paragraphs.Paragraph(text.ReplaceLineEndings(" "));

    public string Read(XElement node) =>
        string.Join(" ", node.Elements(Namespaces.Text + "p").Select(paragraph => paragraphs.Line(paragraph).ReplaceLineEndings(" ")));
}
