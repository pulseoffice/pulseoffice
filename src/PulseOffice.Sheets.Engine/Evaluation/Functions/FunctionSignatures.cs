namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public interface IFunctionSignatures
{
    bool Knows(string name);

    IReadOnlyList<string> Arguments(string name);

    string ArgumentName(string name, int index);
}

public sealed class FunctionSignatures : IFunctionSignatures
{
    private const string ResourceName = "PulseOffice.Sheets.Engine.FunctionSignatures.txt";

    private readonly Dictionary<string, string[]> _arguments = new(StringComparer.OrdinalIgnoreCase);

    public FunctionSignatures()
    {
        using var stream = typeof(FunctionSignatures).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing resource {ResourceName}.");
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            var bar = line.IndexOf('|');
            if (bar <= 0) continue;
            var arguments = line[(bar + 1)..].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            _arguments[line[..bar].Trim()] = arguments;
        }
    }

    public bool Knows(string name) => _arguments.ContainsKey(name);

    public IReadOnlyList<string> Arguments(string name) => _arguments.GetValueOrDefault(name) ?? [];

    public string ArgumentName(string name, int index)
    {
        var arguments = Arguments(name);
        if (index < arguments.Count) return arguments[index];
        var repeated = arguments.Reverse().TakeWhile(argument => argument.EndsWith('2')).Reverse().ToArray();
        if (repeated.Length == 0) return string.Empty;
        var beyond = index - arguments.Count;
        var stem = repeated[beyond % repeated.Length];
        return stem[..^1] + (3 + beyond / repeated.Length);
    }
}
