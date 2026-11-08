using System.Xml.Linq;
using PulseOffice.Foundation.Diagnostics;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdsGroupedObjects
{
    private readonly OdfVectorPictures _vectors = new();

    public Diagnostic Loss { get; } = Diagnostic.Warning("ODF193",
        "Charts, embedded objects, vector pictures and controls inside groups are not imported in spreadsheets: the group keeps its shapes, lines and pictures.");

    public bool Present(XDocument content) =>
        content.Descendants(Namespaces.Table + "table").Descendants(Namespaces.Draw + "g")
            .Any(group => group.Descendants().Any(Unsupported));

    private bool Unsupported(XElement element) =>
        element.Name == Namespaces.Draw + "control"
        || element.Name == Namespaces.Draw + "frame" && element.Elements().Any(child =>
            child.Name == Namespaces.Draw + "object" || child.Name == Namespaces.Draw + "object-ole")
        || element.Name == Namespaces.Draw + "frame" && element.Elements(Namespaces.Draw + "image").ToList() is { Count: > 0 } images
           && images.All(_vectors.Declares);
}
