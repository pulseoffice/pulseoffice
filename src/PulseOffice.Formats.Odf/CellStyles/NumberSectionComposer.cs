namespace PulseOffice.Formats.Odf;

internal sealed class NumberSectionComposer
{
    private const string Prefix = "value()";

    public string Compose(string main, IReadOnlyList<(string Condition, string Code)> mapped)
    {
        if (mapped.Count == 0) return main;

        string? Find(string condition) => mapped.FirstOrDefault(map => map.Condition == Prefix + condition).Code;

        if (mapped.Count == 1 && Find("<0") is { } negative) return main + ";" + negative;
        if (mapped.Count == 1 && Find(">=0") is { } nonNegative) return nonNegative + ";" + main;
        if (mapped.Count == 2 && Find(">0") is { } positive && Find("<0") is { } below) return positive + ";" + below + ";" + main;
        if (mapped.Count == 2 && Find("<0") is { } minus && Find("=0") is { } zero) return main + ";" + minus + ";" + zero;

        var sections = mapped.Select(map => Bracketed(map.Condition) + map.Code).ToList();
        sections.Add(main);
        return string.Join(';', sections.Take(4));
    }

    private string Bracketed(string condition) =>
        condition.StartsWith(Prefix, StringComparison.Ordinal) ? "[" + condition[Prefix.Length..].Replace("!=", "<>", StringComparison.Ordinal) + "]" : string.Empty;
}
