using System.Globalization;
using System.Xml.Linq;

namespace PulseOffice.Formats.OpenXml.Diagrams;

internal sealed record DiagramPoint(string Id, string Type, XElement? Text, XElement? ShapeProperties = null)
{
    public bool IsNode => Type is "node" or "asst";
}

internal sealed record DiagramPresentation(string AssociatedId, string Name, string? StyleLabel, int StyleIndex, int StyleCount, IReadOnlyDictionary<string, string> Variables)
{
    public XElement? ShapeProperties { get; init; }
}

internal sealed class DiagramDataModel
{
    private readonly Dictionary<string, DiagramPoint> _points;
    private readonly Dictionary<string, List<string>> _children;
    private readonly Dictionary<string, string> _parents;
    private readonly Dictionary<string, string> _siblingTransitions;
    private readonly Dictionary<string, string> _parentTransitions;
    private readonly List<DiagramPresentation> _presentations;

    public DiagramDataModel(
        Dictionary<string, DiagramPoint> points,
        Dictionary<string, List<string>> children,
        Dictionary<string, string> siblingTransitions,
        Dictionary<string, string> parentTransitions,
        List<DiagramPresentation> presentations,
        DiagramPoint root)
    {
        _points = points;
        _siblingTransitions = siblingTransitions;
        _parentTransitions = parentTransitions;
        _presentations = presentations;
        (_children, _parents) = TreeFrom(root, children);
        Root = root;
    }

    public const int MaxDepthOfData = 128;

    private (Dictionary<string, List<string>> Children, Dictionary<string, string> Parents) TreeFrom(DiagramPoint root, Dictionary<string, List<string>> children)
    {
        var tree = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal) { root.Id };
        var pending = new Queue<(string Id, int Depth)>();
        pending.Enqueue((root.Id, 0));
        while (pending.Count > 0)
        {
            var (id, depth) = pending.Dequeue();
            if (depth >= MaxDepthOfData || !children.TryGetValue(id, out var candidates)) continue;
            var kept = new List<string>();
            foreach (var child in candidates)
            {
                if (!visited.Add(child)) continue;
                kept.Add(child);
                parents[child] = id;
                pending.Enqueue((child, depth + 1));
            }

            if (kept.Count > 0) tree[id] = kept;
        }

        return (tree, parents);
    }

    public DiagramPoint Root { get; }

    public DiagramPoint? Point(string id) => _points.GetValueOrDefault(id);

    public IReadOnlyList<DiagramPoint> Children(DiagramPoint point) =>
        _children.TryGetValue(point.Id, out var ids) ? [.. ids.Select(Point).OfType<DiagramPoint>()] : [];

    public DiagramPoint? Parent(DiagramPoint point) =>
        _parents.TryGetValue(point.Id, out var parent) ? Point(parent) : null;

    public DiagramPoint? SiblingTransition(DiagramPoint point) =>
        _siblingTransitions.TryGetValue(point.Id, out var id) ? Point(id) : null;

    public DiagramPoint? ParentTransition(DiagramPoint point) =>
        _parentTransitions.TryGetValue(point.Id, out var id) ? Point(id) : null;

    public IReadOnlyList<DiagramPoint> Descendants(DiagramPoint point) =>
        [.. Children(point).SelectMany(child => (IEnumerable<DiagramPoint>)[child, .. Descendants(child)])];

    public int Depth(DiagramPoint point)
    {
        var depth = 0;
        for (var current = Parent(point); current is not null; current = Parent(current)) depth++;
        return depth;
    }

    public int MaxDepth(DiagramPoint point)
    {
        var children = Children(point).Where(child => child.IsNode).ToList();
        return children.Count == 0 ? 0 : 1 + children.Max(MaxDepth);
    }

    public DiagramPresentation? Presentation(DiagramPoint point, string name) =>
        _presentations.FirstOrDefault(item => item.AssociatedId == point.Id && item.Name == name);

    public void ReplaceText(string pointId, XElement text)
    {
        if (_points.TryGetValue(pointId, out var point)) _points[pointId] = point with { Text = text };
    }
}

internal sealed class DiagramDataModelReader
{
    private static readonly XNamespace Diagram = "http://schemas.openxmlformats.org/drawingml/2006/diagram";

    public DiagramDataModel? Read(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var model = document.Root;
        if (model is null) return null;
        var points = new Dictionary<string, DiagramPoint>(StringComparer.Ordinal);
        var presentations = new List<DiagramPresentation>();
        foreach (var element in model.Element(Diagram + "ptLst")?.Elements(Diagram + "pt") ?? [])
        {
            if ((string?)element.Attribute("modelId") is not { } id) continue;
            var type = (string?)element.Attribute("type") ?? "node";
            points[id] = new DiagramPoint(id, type, element.Element(Diagram + "t"), element.Element(Diagram + "spPr") is { HasElements: true } properties ? properties : null);
            if (type == "pres" && element.Element(Diagram + "prSet") is { } set && (string?)set.Attribute("presAssocID") is { } associated)
                presentations.Add(new DiagramPresentation(associated, (string?)set.Attribute("presName") ?? string.Empty,
                    (string?)set.Attribute("presStyleLbl"), Integer(set.Attribute("presStyleIdx")), Integer(set.Attribute("presStyleCnt")),
                    set.Element(Diagram + "presLayoutVars")?.Elements().ToDictionary(variable => variable.Name.LocalName, variable => (string?)variable.Attribute("val") ?? string.Empty)
                        ?? [])
                {
                    ShapeProperties = element.Element(Diagram + "spPr") is { HasElements: true } own ? own : null,
                });
        }

        var children = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var siblings = new Dictionary<string, string>(StringComparer.Ordinal);
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        var ordered = (model.Element(Diagram + "cxnLst")?.Elements(Diagram + "cxn") ?? [])
            .Where(connection => ((string?)connection.Attribute("type") ?? "parOf") == "parOf")
            .Select(connection => (Source: (string?)connection.Attribute("srcId"), Destination: (string?)connection.Attribute("destId"),
                Order: Integer(connection.Attribute("srcOrd")), Sibling: (string?)connection.Attribute("sibTransId"), Parent: (string?)connection.Attribute("parTransId")))
            .Where(connection => connection.Source is not null && connection.Destination is not null && points.ContainsKey(connection.Source) && points.ContainsKey(connection.Destination))
            .OrderBy(connection => connection.Order);
        foreach (var connection in ordered)
        {
            if (!children.TryGetValue(connection.Source!, out var list)) children[connection.Source!] = list = [];
            list.Add(connection.Destination!);
            if (connection.Sibling is { } sibling && points.ContainsKey(sibling)) siblings[connection.Destination!] = sibling;
            if (connection.Parent is { } parent && points.ContainsKey(parent)) parents[connection.Destination!] = parent;
        }

        var root = points.Values.FirstOrDefault(point => point.Type == "doc");
        return root is null ? null : new DiagramDataModel(points, children, siblings, parents, presentations, root);
    }

    private int Integer(XAttribute? attribute) =>
        int.TryParse((string?)attribute, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
