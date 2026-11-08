namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public interface IFunctionRegistry
{
    FunctionDefinition? Find(string name);
    bool Contains(string name);
    int Count { get; }
    IReadOnlyCollection<FunctionDefinition> All { get; }

    IEnumerable<FunctionDefinition> InCategory(string category);
    IEnumerable<string> Categories { get; }
}

public sealed class FunctionRegistry : IFunctionRegistry
{
    private readonly Dictionary<string, FunctionDefinition> _functions;

    public FunctionRegistry(IEnumerable<IFunctionProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _functions = new Dictionary<string, FunctionDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in providers)
            foreach (var function in provider.GetFunctions())
            {
                if (!_functions.TryAdd(function.Name, function))
                    throw new InvalidOperationException(
                        $"La funzione '{function.Name}' è dichiarata da più provider " +
                        $"(categoria in conflitto: '{provider.Category}').");
            }
    }

    public FunctionDefinition? Find(string name) => _functions.GetValueOrDefault(name);
    public bool Contains(string name) => _functions.ContainsKey(name);
    public int Count => _functions.Count;
    public IReadOnlyCollection<FunctionDefinition> All => _functions.Values;

    public IEnumerable<FunctionDefinition> InCategory(string category) =>
        _functions.Values.Where(f => string.Equals(f.Category, category, StringComparison.Ordinal));

    public IEnumerable<string> Categories =>
        _functions.Values.Select(f => f.Category).Distinct().Order(StringComparer.CurrentCulture);
}
