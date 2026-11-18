using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Sheets;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace PulseOffice.Formats.OpenXml;

public sealed class XlsxObjectLinks
{
    private readonly HyperlinkTargets _targets = new();

    public void Write(DrawingsPart part, IReadOnlyList<PlacedObject> objects)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(objects);
        var links = objects.Where(placed => placed.Hyperlink is { Length: > 0 })
            .GroupBy(placed => placed.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        if (links.Count == 0) return;
        foreach (var properties in Properties(part))
        {
            if (properties.Name?.Value is not { } name || !links.TryGetValue(name, out var placed)
                || !Uri.TryCreate(placed.Hyperlink, UriKind.RelativeOrAbsolute, out var uri))
                continue;
            properties.RemoveAllChildren<A.HyperlinkOnClick>();
            var click = new A.HyperlinkOnClick { Id = part.AddHyperlinkRelationship(uri, true).Id };
            if (placed.HyperlinkTip is { Length: > 0 } tip) click.Tooltip = tip;
            properties.PrependChild(click);
        }
    }

    public IReadOnlyDictionary<string, string> Read(DrawingsPart? part)
    {
        var links = new Dictionary<string, string>(StringComparer.Ordinal);
        if (part is null) return links;
        foreach (var properties in Properties(part))
            if (properties.Name?.Value is { } name
                && properties.GetFirstChild<A.HyperlinkOnClick>()?.Id?.Value is { Length: > 0 } relationship
                && _targets.Of(part, relationship) is { Length: > 0 } target)
                links.TryAdd(name, target);
        return links;
    }

    public IReadOnlyDictionary<string, string> ReadTips(DrawingsPart? part)
    {
        var tips = new Dictionary<string, string>(StringComparer.Ordinal);
        if (part is null) return tips;
        foreach (var properties in Properties(part))
            if (properties.Name?.Value is { } name && properties.GetFirstChild<A.HyperlinkOnClick>()?.Tooltip?.Value is { Length: > 0 } tip)
                tips.TryAdd(name, tip);
        return tips;
    }

    private IEnumerable<Xdr.NonVisualDrawingProperties> Properties(DrawingsPart part) =>
        part.WorksheetDrawing?.Descendants<Xdr.NonVisualDrawingProperties>() ?? [];
}
