namespace PulseOffice.Document.Metadata;

public sealed class KeywordListText
{
    public string Join(IReadOnlyList<string> keywords)
    {
        ArgumentNullException.ThrowIfNull(keywords);
        return string.Join(keywords.Any(keyword => keyword.Contains(',', StringComparison.Ordinal)) ? "; " : ", ", keywords);
    }

    public IReadOnlyList<string> Split(string? text) =>
        text is { Length: > 0 }
            ? text.Split(text.Contains(';', StringComparison.Ordinal) ? ';' : ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
}
