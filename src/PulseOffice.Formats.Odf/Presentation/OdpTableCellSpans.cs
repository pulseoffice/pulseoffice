using System.Xml.Linq;
using PulseOffice.Foundation.Diagnostics;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpTableCellSpans
{
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;
    private static readonly XNamespace Table = OdfPackage.Namespaces.Table;
    private static readonly XNamespace Text = OdfPackage.Namespaces.Text;

    public Diagnostic Loss { get; } = Diagnostic.Info("ODF281",
        "Some table cells format part of their text with their own text style: a Deck table cell has one look, set by its cell style, so that formatting is lost.");

    public bool Present(XContainer? body) =>
        body?.Descendants(Draw + "frame").Elements(Table + "table").Descendants(Table + "table-cell")
            .Any(cell => cell.Descendants(Text + "span").Any(span => span.Attribute(Text + "style-name") is not null)) == true;
}
