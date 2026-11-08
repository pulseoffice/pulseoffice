namespace PulseOffice.Sheets.Engine.Evaluation;

public sealed class EvaluationScope
{
    private readonly Dictionary<string, EvalValue> _bindings;
    private readonly EvaluationScope? _parent;

    private EvaluationScope(EvaluationScope? parent, int capacity)
    {
        _parent = parent;
        _bindings = new Dictionary<string, EvalValue>(capacity, StringComparer.OrdinalIgnoreCase);
    }

    public static readonly EvaluationScope Empty = new(null, 0);

    public int Depth => _parent is null ? 0 : _parent.Depth + 1;

    public EvaluationScope Extend(int capacity = 4) => new(this, capacity);

    public EvaluationScope Bind(string name, EvalValue value)
    {
        _bindings[Canonical(name)] = value;
        return this;
    }

    public bool TryResolve(string name, out EvalValue value)
    {
        for (var scope = this; scope is not null; scope = scope._parent)
            if (scope._bindings.TryGetValue(Canonical(name), out value!))
                return true;

        value = null!;
        return false;
    }

    private static readonly string[] ExcelPrefixes = ["_xlpm.", "_xlop."];

    private string Canonical(string name)
    {
        foreach (var prefix in ExcelPrefixes)
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return name[prefix.Length..];
        return name;
    }
}
