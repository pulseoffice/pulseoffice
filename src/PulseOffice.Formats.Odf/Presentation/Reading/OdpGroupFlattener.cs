using System.Xml.Linq;
using PulseOffice.Formats.Packaging;

namespace PulseOffice.Formats.Odf;

internal sealed record OdpGroupShape(string Key, string? Name, IReadOnlyList<string> Children, double RotationDegrees = 0);

internal sealed class OdpGroupMembers(IReadOnlyDictionary<string, IReadOnlyList<string>> members, IReadOnlyList<OdpGroupShape> shapes)
{
    public IReadOnlyList<OdpGroupShape> Shapes => shapes;

    public IReadOnlyList<string> Of(string group) => members.GetValueOrDefault(group) ?? [];
}

internal sealed class OdpGroupFlattener
{
    private const int MaximumDepth = 64;

    private readonly XName _group = OdfPackage.Namespaces.Draw + "g";
    private readonly OdpElementVisibility _visibility = new();
    private readonly XName _name = OdfPackage.Namespaces.Draw + "name";
    private readonly XName _xmlId = OdfPackage.Namespaces.Xml + "id";
    private readonly XName _drawId = OdfPackage.Namespaces.Draw + "id";
    private readonly XName _rotation = XNamespace.Get("urn:pulseoffice:presentation-group") + "rotation";

    public XElement Flatten(XElement page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var members = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var shapes = new List<OdpGroupShape>();
        var anonymous = 0;
        foreach (var group in page.Elements(_group).ToList()) Collect(group, members, shapes, ref anonymous, 0);

        while (page.Elements(_group).FirstOrDefault() is { } group)
        {
            var children = group.Nodes().ToList();
            var hidden = _visibility.Hidden(group);
            foreach (var child in children)
            {
                child.Remove();
                if (hidden && child is XElement element) _visibility.Inherit(element);
            }
            group.ReplaceWith(children);
        }

        page.AddAnnotation(new OdpGroupMembers(members, shapes));
        return page;
    }

    private (string Key, List<string> Leaves) Collect(XElement group, Dictionary<string, IReadOnlyList<string>> members, List<OdpGroupShape> shapes, ref int anonymous, int depth)
    {
        var leaves = new List<string>();
        var direct = new List<string>();
        var groupId = IdOf(group);
        var key = groupId ?? $"pulseoffice-group-{++anonymous}";
        var index = 0;
        foreach (var child in group.Elements())
        {
            index++;
            if (child.Name == _group && depth >= MaximumDepth) continue;
            if (child.Name == _group)
            {
                var nested = Collect(child, members, shapes, ref anonymous, depth + 1);
                leaves.AddRange(nested.Leaves);
                direct.Add(nested.Key);
                continue;
            }
            if (IdOf(child) is not { } id)
            {
                id = $"{key}.{index}";
                child.SetAttributeValue(_drawId, id);
            }
            leaves.Add(id);
            direct.Add(id);
        }
        if (groupId is not null) members[groupId] = leaves;
        var rotation = double.TryParse((string?)group.Attribute(_rotation), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var degrees) && double.IsFinite(degrees) ? degrees : 0;
        shapes.Add(new OdpGroupShape(key, (string?)group.Attribute(_name), direct, rotation));
        return (key, leaves);
    }

    private string? IdOf(XElement element) => (string?)element.Attribute(_xmlId) ?? (string?)element.Attribute(_drawId);
}
