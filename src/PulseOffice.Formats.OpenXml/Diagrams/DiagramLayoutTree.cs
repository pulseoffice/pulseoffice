using System.Globalization;
using System.Xml.Linq;

namespace PulseOffice.Formats.OpenXml.Diagrams;

internal sealed record DiagramRect(double X, double Y, double Width, double Height)
{
    public double CentreX => X + Width / 2;

    public double CentreY => Y + Height / 2;
}

internal sealed record DiagramParagraph(XElement Source, int Level);

internal sealed class DiagramBox(string name, DiagramPoint point)
{
    public string Name { get; } = name;

    public DiagramPoint Point { get; } = point;

    public string? StyleLabel { get; set; }

    public int StyleIndex { get; set; }

    public int StyleCount { get; set; } = 1;

    public string ChildOrder { get; set; } = "b";

    public DiagramAlgorithmAtom Algorithm { get; set; } = new("composite", new Dictionary<string, string>());

    public DiagramShapeAtom? Shape { get; set; }

    public List<DiagramConstraint> Constraints { get; } = [];

    public List<DiagramRule> Rules { get; } = [];

    public Dictionary<string, string> Variables { get; } = new(StringComparer.Ordinal);

    public List<DiagramParagraph> Paragraphs { get; } = [];

    public List<DiagramBox> Children { get; } = [];

    public DiagramRect Bounds { get; set; } = new(0, 0, 0, 0);

    public double Rotation { get; set; }

    public double FontSize { get; set; }

    public double MinimumFontSize { get; set; } = 5;

    public string? PresetOverride { get; set; }

    public XElement? CustomProperties { get; set; }

    public bool HasGeometry => Shape is { HideGeometry: false, Type.Length: > 0 } shape && shape.Type != "none";

    public bool HasText => Paragraphs.Count > 0 && Paragraphs.Any(paragraph => paragraph.Source.Value.Length > 0);

    public bool IsConnector => Shape?.Type == "conn" || Algorithm.Type == "conn";

    public bool IsSpacer => Algorithm.Type == "sp" && !HasGeometry && Children.Count == 0;

    public IEnumerable<DiagramBox> SelfAndDescendants() => Children.SelectMany(child => child.SelfAndDescendants()).Prepend(this);
}

internal sealed class DiagramTreeBuilder(DiagramDataModel model)
{
    private const int DepthLimit = 48;
    public const int WorkLimit = 20000;
    private int _work;
    private static readonly XNamespace Drawing = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private readonly Dictionary<string, DiagramForEachAtom> _namedLoops = new(StringComparer.Ordinal);

    public DiagramBox Build(DiagramLayoutNodeAtom root)
    {
        ArgumentNullException.ThrowIfNull(root);
        Register(root);
        return Node(root, model.Root, 0);
    }

    private void Register(DiagramAtom atom)
    {
        var children = atom switch
        {
            DiagramLayoutNodeAtom node => node.Children,
            DiagramForEachAtom loop => loop.Children,
            DiagramChooseAtom choose => [.. choose.Branches.SelectMany(branch => branch.Children), .. choose.Otherwise],
            _ => [],
        };
        if (atom is DiagramForEachAtom { Name.Length: > 0 } named) _namedLoops.TryAdd(named.Name, named);
        foreach (var child in children) Register(child);
    }

    private DiagramBox Node(DiagramLayoutNodeAtom atom, DiagramPoint point, int depth)
    {
        var box = new DiagramBox(atom.Name, point) { StyleLabel = atom.StyleLabel, ChildOrder = atom.ChildOrder };
        _work++;
        if (model.Presentation(point, atom.Name) is { } presentation)
        {
            box.StyleLabel = presentation.StyleLabel ?? box.StyleLabel;
            box.StyleIndex = presentation.StyleIndex;
            box.StyleCount = Math.Max(1, presentation.StyleCount);
            foreach (var (key, value) in presentation.Variables) box.Variables[key] = value;
            box.CustomProperties = presentation.ShapeProperties;
        }
        else if (model.Parent(point) is { } parent)
        {
            var siblings = model.Children(parent).Where(sibling => sibling.Type == point.Type).ToList();
            box.StyleIndex = Math.Max(0, siblings.IndexOf(point));
            box.StyleCount = Math.Max(1, siblings.Count);
        }
        box.CustomProperties ??= point.ShapeProperties;
        if (depth < DepthLimit && _work < WorkLimit) Expand(box, atom.Children, point, depth);
        return box;
    }

    private void Expand(DiagramBox box, IReadOnlyList<DiagramAtom> atoms, DiagramPoint point, int depth)
    {
        foreach (var atom in atoms)
        {
            switch (atom)
            {
                case DiagramAlgorithmAtom algorithm: box.Algorithm = algorithm; break;
                case DiagramShapeAtom shape: box.Shape = shape; break;
                case DiagramPresOfAtom presOf: Text(box, presOf, point); break;
                case DiagramConstraintsAtom constraints: box.Constraints.AddRange(constraints.Constraints); break;
                case DiagramRulesAtom rules: box.Rules.AddRange(rules.Rules); break;
                case DiagramVariablesAtom variables:
                    foreach (var (key, value) in variables.Variables) box.Variables.TryAdd(key, value);
                    break;
                case DiagramLayoutNodeAtom node:
                    if (_work < WorkLimit) box.Children.Add(Node(node, point, depth + 1));
                    break;
                case DiagramForEachAtom loop:
                {
                    if (depth >= DepthLimit || _work >= WorkLimit) break;
                    var effective = loop.Reference is { } reference && _namedLoops.TryGetValue(reference, out var target) ? target : loop;
                    foreach (var selected in Select(point, effective.Axes, effective.PointTypes, effective.Count, effective.Start, effective.Step, effective.HideLastTransition))
                    {
                        if (++_work >= WorkLimit) break;
                        Expand(box, effective.Children, selected, depth + 1);
                    }
                    break;
                }
                case DiagramChooseAtom choose:
                {
                    var branch = choose.Branches.FirstOrDefault(condition => Holds(condition, box, point));
                    Expand(box, branch?.Children ?? choose.Otherwise, point, depth);
                    break;
                }
            }
        }
    }

    private void Text(DiagramBox box, DiagramPresOfAtom presOf, DiagramPoint point)
    {
        if (presOf.Axes.Count == 0) return;
        var axis = presOf.Axes[0];
        var baseDepth = model.Depth(point);
        IEnumerable<DiagramPoint> sources = axis switch
        {
            "desOrSelf" => [point, .. model.Descendants(point)],
            "des" => model.Descendants(point),
            "ch" => model.Children(point),
            _ => [point],
        };
        var type = presOf.PointTypes.Count > 0 ? presOf.PointTypes[0] : "all";
        foreach (var source in sources.Where(source => Matches(source, type)))
            foreach (var paragraph in source.Text?.Elements(Drawing + "p") ?? [])
                box.Paragraphs.Add(new DiagramParagraph(paragraph, model.Depth(source) - baseDepth));
    }

    private bool Holds(DiagramConditionAtom condition, DiagramBox box, DiagramPoint point)
    {
        double actual;
        switch (condition.Function)
        {
            case "var":
            {
                var value = box.Variables.GetValueOrDefault(condition.Argument) is { Length: > 0 } set ? set : Default(condition.Argument);
                return Compare(value, condition.Operator, condition.Value);
            }
            case "cnt": actual = Select(point, condition.Axes, condition.PointTypes, 0, 1, 1, false).Count; break;
            case "maxDepth": actual = Select(point, condition.Axes, condition.PointTypes, 0, 1, 1, false).Select(model.MaxDepth).DefaultIfEmpty(0).Max(); break;
            case "depth": actual = model.Depth(point); break;
            case "pos":
            case "revPos":
            case "posEven":
            case "posOdd":
            {
                var siblings = model.Parent(point) is { } parent ? model.Children(parent).Where(sibling => sibling.Type == point.Type).ToList() : [point];
                var position = siblings.IndexOf(point) + 1;
                actual = condition.Function switch
                {
                    "revPos" => siblings.Count - position + 1,
                    "posEven" => position % 2 == 0 ? 1 : 0,
                    "posOdd" => position % 2 == 1 ? 1 : 0,
                    _ => position,
                };
                break;
            }
            default: return false;
        }
        return double.TryParse(condition.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var expected)
            ? Compare(actual, condition.Operator, expected)
            : Compare(actual.ToString(CultureInfo.InvariantCulture), condition.Operator, condition.Value);
    }

    private string Default(string variable) => variable switch
    {
        "dir" => "norm",
        "hierBranch" => "std",
        "animLvl" or "animOne" => "none",
        "orgChart" or "bulletEnabled" => "0",
        _ => string.Empty,
    };

    private bool Compare(double actual, string op, double expected) => op switch
    {
        "equ" => Math.Abs(actual - expected) < 1e-9,
        "neq" => Math.Abs(actual - expected) >= 1e-9,
        "gt" => actual > expected,
        "lt" => actual < expected,
        "gte" => actual >= expected,
        "lte" => actual <= expected,
        _ => false,
    };

    private bool Compare(string actual, string op, string expected) => op switch
    {
        "equ" => string.Equals(actual, expected, StringComparison.Ordinal),
        "neq" => !string.Equals(actual, expected, StringComparison.Ordinal),
        _ => false,
    };

    private List<DiagramPoint> Select(DiagramPoint origin, IReadOnlyList<string> axes, IReadOnlyList<string> types, int count, int start, int step, bool hideLast)
    {
        IEnumerable<DiagramPoint> current = [origin];
        var steps = Math.Max(axes.Count, 1);
        for (var index = 0; index < steps; index++)
        {
            var axis = index < axes.Count ? axes[index] : "self";
            var type = index < types.Count ? types[index] : types.Count > 0 ? types[^1] : "all";
            current = [.. current.SelectMany(point => Axis(point, axis, type, hideLast)).Where(point => Matches(point, type))];
        }
        var list = current.Distinct().ToList();
        var skipped = list.Skip(Math.Max(0, start - 1)).Where((_, position) => step <= 1 || position % step == 0);
        return [.. count > 0 ? skipped.Take(count) : skipped];
    }

    private IEnumerable<DiagramPoint> Axis(DiagramPoint point, string axis, string type, bool hideLast)
    {
        switch (axis)
        {
            case "self": return [point];
            case "ch": return type is "sibTrans" ? model.Children(point).Select(model.SiblingTransition).OfType<DiagramPoint>()
                : type is "parTrans" ? model.Children(point).Select(model.ParentTransition).OfType<DiagramPoint>()
                : model.Children(point);
            case "des": return model.Descendants(point);
            case "desOrSelf": return [point, .. model.Descendants(point)];
            case "par": return model.Parent(point) is { } parent ? [parent] : [];
            case "ancst": return Ancestors(point);
            case "ancstOrSelf": return [point, .. Ancestors(point)];
            case "root": return [model.Root];
            case "followSib":
            {
                if (type == "sibTrans")
                {
                    if (hideLast && IsLast(point)) return [];
                    return model.SiblingTransition(point) is { } transition ? [transition] : [];
                }
                return Siblings(point).SkipWhile(sibling => sibling != point).Skip(1);
            }
            case "precedSib":
                return Siblings(point).TakeWhile(sibling => sibling != point).Reverse();
            default: return [point];
        }
    }

    private IEnumerable<DiagramPoint> Ancestors(DiagramPoint point)
    {
        for (var current = model.Parent(point); current is not null; current = model.Parent(current)) yield return current;
    }

    private List<DiagramPoint> Siblings(DiagramPoint point) =>
        model.Parent(point) is { } parent ? [.. model.Children(parent)] : [point];

    private bool IsLast(DiagramPoint point)
    {
        var siblings = Siblings(point).Where(sibling => sibling.IsNode).ToList();
        return siblings.Count > 0 && siblings[^1] == point;
    }

    private bool Matches(DiagramPoint point, string type) => type switch
    {
        "all" or "" => true,
        "node" => point.IsNode,
        "nonAsst" => point.Type == "node",
        "nonNorm" => point.Type != "node",
        "norm" => point.Type == "node",
        _ => point.Type == type,
    };
}
