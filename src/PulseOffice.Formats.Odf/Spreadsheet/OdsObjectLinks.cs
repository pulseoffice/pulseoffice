using System.Xml.Linq;
using PulseOffice.Sheets;
using Namespaces = PulseOffice.Formats.Packaging.OdfPackage.Namespaces;

namespace PulseOffice.Formats.Odf;

internal sealed class OdsObjectLinkTable(IReadOnlyDictionary<string, string> links)
{
    public IReadOnlyDictionary<string, string> Links => links;
}

internal sealed class OdsObjectLinks
{
    private readonly XName _anchor = Namespaces.Draw + "a";
    private readonly XName _name = Namespaces.Draw + "name";
    private readonly XName _href = Namespaces.Xlink + "href";
    private readonly HashSet<XName> _holders = [Namespaces.Table + "table-cell", Namespaces.Table + "covered-table-cell", Namespaces.Table + "shapes"];
    private readonly HashSet<XName> _linkedText =
    [
        Namespaces.Draw + "custom-shape", Namespaces.Draw + "rect", Namespaces.Draw + "ellipse", Namespaces.Draw + "circle",
        Namespaces.Draw + "line", Namespaces.Draw + "text-box",
    ];

    public PulseOffice.Foundation.Diagnostics.Diagnostic Loss { get; } = PulseOffice.Foundation.Diagnostics.Diagnostic.Warning("ODF194",
        "Hyperlinks inside the text of paths, polygons, connectors and other drawing objects of spreadsheets are not imported: the text is kept without its links. Hyperlinks in the text of shapes and text boxes, and on whole shapes and pictures, are kept.");

    public bool Present(XDocument content) =>
        content.Descendants(Namespaces.Table + "table").Descendants()
            .Any(element => element.Name.Namespace == Namespaces.Draw && element.Name != _anchor && !_linkedText.Contains(element.Name) && element.Elements(Namespaces.Text + "p").Descendants(Namespaces.Text + "a").Any());

    public void Wrap(XElement container, IReadOnlyDictionary<string, string> links)
    {
        if (links.Count == 0) return;
        foreach (var shape in container.Descendants().Where(element => element.Parent is { } parent && _holders.Contains(parent.Name)).ToList())
        {
            if ((string?)shape.Attribute(_name) is not { } name || !links.TryGetValue(name, out var target)) continue;
            var anchor = new XElement(_anchor,
                new XAttribute(Namespaces.Xlink + "type", "simple"),
                new XAttribute(_href, target));
            shape.ReplaceWith(anchor);
            anchor.Add(shape);
        }
    }

    public void Unwrap(XElement table)
    {
        var links = new Dictionary<string, string>(StringComparer.Ordinal);
        var unnamed = 0;
        foreach (var anchor in table.Descendants(_anchor).Where(anchor => anchor.Parent is { } parent && _holders.Contains(parent.Name)).ToList())
        {
            var href = (string?)anchor.Attribute(_href);
            var shapes = anchor.Elements().Where(element => element.Name.Namespace == Namespaces.Draw).ToList();
            foreach (var shape in shapes)
            {
                if (string.IsNullOrEmpty(href)) break;
                if ((string?)shape.Attribute(_name) is not { Length: > 0 } name)
                {
                    name = $"Linked object {++unnamed}";
                    shape.SetAttributeValue(_name, name);
                }
                links.TryAdd(name, href);
            }
            anchor.ReplaceWith(anchor.Nodes());
        }
        table.AddAnnotation(new OdsObjectLinkTable(links));
    }

    public void Apply(XElement table, Worksheet sheet)
    {
        if (table.Annotation<OdsObjectLinkTable>() is not { Links.Count: > 0 } found) return;
        for (var index = 0; index < sheet.Objects.Count; index++)
            if (found.Links.TryGetValue(sheet.Objects[index].Id, out var link))
                sheet.Objects[index] = sheet.Objects[index] with { Hyperlink = link };
    }
}

internal sealed class ObjectLinkPartReader(OdsObjectLinks links) : IOdsSheetPartReader
{
    public void Read(XElement table, Worksheet sheet, OdsReadContext context) => links.Apply(table, sheet);
}
