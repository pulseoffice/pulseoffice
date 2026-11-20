namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public interface IFunctionArgumentNames
{
    bool Knows(string argument);

    string Localize(string argument, string language);
}

public sealed class FunctionArgumentNames : IFunctionArgumentNames
{
    private const string ResourceName = "PulseOffice.Sheets.Engine.FunctionArgumentNames.txt";
    private static readonly string[] Languages = ["it", "de", "es", "fr"];

    private readonly Dictionary<string, string[]> _names = new(StringComparer.OrdinalIgnoreCase);

    public FunctionArgumentNames()
    {
        using var stream = typeof(FunctionArgumentNames).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing resource {ResourceName}.");
        using var reader = new StreamReader(stream);
        while (reader.ReadLine() is { } line)
        {
            var fields = line.Split('|', StringSplitOptions.TrimEntries);
            if (fields.Length == Languages.Length + 1 && fields[0].Length > 0) _names[fields[0]] = fields[1..];
        }
    }

    public bool Knows(string argument) => _names.ContainsKey(Stem(argument));

    private static string Stem(string argument) => argument.TrimEnd("0123456789".ToCharArray());

    public string Localize(string argument, string language)
    {
        var column = Array.IndexOf(Languages, language);
        if (column < 0 || argument.Length == 0) return argument;
        var stemLength = argument.Length;
        while (stemLength > 0 && char.IsAsciiDigit(argument[stemLength - 1])) stemLength--;
        if (stemLength == 0 || !_names.TryGetValue(argument[..stemLength], out var names)) return argument;
        return names[column] + argument[stemLength..];
    }
}
