namespace PulseOffice.Formats.OpenXml.Diagrams;

internal sealed class DiagramProperties
{
    private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);

    public double? this[string type] => _values.TryGetValue(type, out var value) ? value : null;

    public double Or(string type, double fallback) => this[type] ?? fallback;

    public void Apply(string type, string op, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return;
        _values[type] = op switch
        {
            "lte" when _values.TryGetValue(type, out var current) => Math.Min(current, value),
            "gte" when _values.TryGetValue(type, out var current) => Math.Max(current, value),
            _ => value,
        };
    }

    public bool Has(string type) => _values.ContainsKey(type);
}

internal sealed record DiagramResolvedConstraint(string ForName, string PointType, string Type, string Operator, double Value);

internal sealed class DiagramLayoutContext(DiagramBox box, IReadOnlyList<DiagramResolvedConstraint> inherited)
{
    public DiagramBox Box { get; } = box;

    public DiagramProperties Self { get; } = new();

    public Dictionary<DiagramBox, DiagramProperties> Children { get; } = [];

    public IReadOnlyList<DiagramResolvedConstraint> Inherited { get; } = inherited;

    public List<DiagramResolvedConstraint> Descendants { get; } = [];

    public IReadOnlyList<DiagramResolvedConstraint> PassedDown => [.. Inherited, .. Descendants];

    public DiagramProperties Of(DiagramBox child) =>
        Children.TryGetValue(child, out var properties) ? properties : Children[child] = new DiagramProperties();
}

internal sealed class DiagramConstraintSolver
{
    private const double EmuPerMillimetre = 36000;
    private readonly IReadOnlySet<string> _lengths = new HashSet<string>(StringComparer.Ordinal)
    {
        "w", "h", "l", "t", "r", "b", "ctrX", "ctrY", "sp", "sibSp", "secSibSp", "lMarg", "rMarg", "tMarg", "bMarg", "begPad", "endPad", "connDist", "wOff", "hOff",
    };

    public DiagramLayoutContext Solve(DiagramBox box, IReadOnlyList<DiagramResolvedConstraint> inherited)
    {
        var context = new DiagramLayoutContext(box, inherited);
        foreach (var resolved in inherited)
            foreach (var child in box.Children.Where(child => (resolved.ForName.Length == 0 || resolved.ForName == child.Name) && Matches(child, resolved.PointType)))
                context.Of(child).Apply(resolved.Type, resolved.Operator, resolved.Value);
        for (var pass = 0; pass < 2; pass++)
        {
            context.Descendants.Clear();
            foreach (var constraint in box.Constraints)
                Apply(context, constraint, ReferenceOf(context, constraint));
        }
        return context;
    }

    private void Apply(DiagramLayoutContext context, DiagramConstraint constraint, double? reference)
    {
        var value = constraint.ReferenceType == "none" || reference is null
            ? Absolute(constraint)
            : reference.Value * constraint.Factor * Conversion(constraint);
        if (value is not { } amount) return;
        if (constraint.For == "des")
            context.Descendants.Add(new DiagramResolvedConstraint(constraint.ForName, constraint.PointType, constraint.Type, constraint.Operator, amount));
        var self = constraint.For == "self" && constraint.ForName.Length == 0;
        if (self || constraint.ForName == context.Box.Name && constraint.For == "self")
        {
            context.Self.Apply(constraint.Type, constraint.Operator, amount);
            return;
        }
        foreach (var child in context.Box.Children.Where(child => Targets(constraint, child)))
            context.Of(child).Apply(constraint.Type, constraint.Operator, amount);
    }

    private double Conversion(DiagramConstraint constraint)
    {
        var fontReference = constraint.ReferenceType is "primFontSz" or "secFontSz";
        var fontTarget = constraint.Type is "primFontSz" or "secFontSz";
        if (fontReference && _lengths.Contains(constraint.Type)) return ((double)PulseOffice.Foundation.Units.Emu.PerPoint);
        if (fontTarget && _lengths.Contains(constraint.ReferenceType)) return 1 / ((double)PulseOffice.Foundation.Units.Emu.PerPoint);
        return 1;
    }

    private double? Absolute(DiagramConstraint constraint)
    {
        if (double.IsNaN(constraint.Value)) return null;
        return _lengths.Contains(constraint.Type) ? constraint.Value * EmuPerMillimetre : constraint.Value;
    }

    private bool Targets(DiagramConstraint constraint, DiagramBox child)
    {
        if (constraint.ForName.Length > 0 && constraint.ForName != child.Name) return false;
        return Matches(child, constraint.PointType);
    }

    private bool Matches(DiagramBox box, string type) => type switch
    {
        "all" or "" => true,
        "node" => box.Point.IsNode,
        _ => box.Point.Type == type,
    };

    private double? ReferenceOf(DiagramLayoutContext context, DiagramConstraint constraint)
    {
        if (constraint.ReferenceType == "none") return null;
        if (constraint.ReferenceFor == "self" && constraint.ReferenceForName.Length == 0)
            return Own(context, constraint.ReferenceType);
        if (constraint.ReferenceFor == "des")
            return context.PassedDown.LastOrDefault(resolved => resolved.Type == constraint.ReferenceType
                && (constraint.ReferenceForName.Length == 0 || resolved.ForName == constraint.ReferenceForName))?.Value;
        var match = context.Box.Children.FirstOrDefault(child =>
            (constraint.ReferenceForName.Length == 0 || child.Name == constraint.ReferenceForName) && Matches(child, constraint.ReferencePointType)
            && context.Children.TryGetValue(child, out var properties) && properties.Has(constraint.ReferenceType));
        if (match is not null) return context.Children[match][constraint.ReferenceType];
        if (constraint.ReferenceForName == context.Box.Name) return Own(context, constraint.ReferenceType);
        return null;
    }

    private double? Own(DiagramLayoutContext context, string type) => type switch
    {
        "w" => context.Self["w"] ?? context.Box.Bounds.Width,
        "h" => context.Self["h"] ?? context.Box.Bounds.Height,
        _ => context.Self[type],
    };
}
