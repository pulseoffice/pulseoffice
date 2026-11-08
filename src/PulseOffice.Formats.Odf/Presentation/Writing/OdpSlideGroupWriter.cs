using System.Xml.Linq;
using PulseOffice.Formats.Packaging;
using PulseOffice.Presentations;

namespace PulseOffice.Formats.Odf;

internal sealed class OdpSlideGroupWriter(ISlideGroupDecomposition decomposition) : SlideGroupWriter<XElement>(decomposition)
{
    private static readonly XNamespace Draw = OdfPackage.Namespaces.Draw;

    private readonly XName _rotation = XNamespace.Get("urn:pulseoffice:presentation-group") + "rotation";

    protected override IReadOnlyList<XElement> InDocumentOrder(IReadOnlyList<XElement> members)
    {
        var root = members[0].AncestorsAndSelf().Last();
        var order = root.DescendantsAndSelf().Select((node, index) => (node, index))
            .ToDictionary(pair => (object)pair.node, pair => pair.index, ReferenceEqualityComparer.Instance);
        return [.. members.OrderBy(member => order.TryGetValue(member, out var index) ? index : int.MaxValue)];
    }

    protected override XElement Enclose(SlideGroup group, IReadOnlyList<XElement> members)
    {
        var shape = new XElement(Draw + "g");
        if (group.Name is { Length: > 0 } name) shape.SetAttributeValue(Draw + "name", name);
        if (Math.Abs(group.RotationDegrees) > 1e-9)
            shape.SetAttributeValue(_rotation, group.RotationDegrees.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        members[0].AddBeforeSelf(shape);
        foreach (var member in members)
        {
            member.Remove();
            shape.Add(member);
        }
        return shape;
    }
}
