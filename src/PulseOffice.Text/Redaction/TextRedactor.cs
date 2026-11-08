using System.Text.RegularExpressions;

namespace PulseOffice.Text.Redaction;

public sealed record RedactionPattern(string Name, string Expression);

public interface ITextRedactor
{
    string Mask { get; }

    IReadOnlyList<RedactionPattern> Patterns { get; }

    string Redact(string text, IEnumerable<RedactionPattern> patterns);
}

public sealed class TextRedactor : ITextRedactor
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(1);

    public string Mask => "■■■■";

    public IReadOnlyList<RedactionPattern> Patterns { get; } =
    [
        new("email", @"[\w.+-]+@[\w-]+\.[\w.-]+"),
        new("iban", @"(?<![A-Za-z0-9])[A-Za-z]{2}\d{2}(?: ?[A-Za-z0-9]){10,30}(?![A-Za-z0-9])"),
        new("card", @"(?<![\w-])\d(?:[ -]?\d){12,18}(?![\w-])"),
        new("fiscalCode", @"(?<![A-Za-z0-9])[A-Za-z]{6}\d{2}[A-Za-z]\d{2}[A-Za-z]\d{3}[A-Za-z](?![A-Za-z0-9])"),
        new("phone", @"(?<![\w+])(?:\+\d{8,15}(?![\w-])|(?!\d{1,2}([./-])\d{1,2}\1\d{2,4}(?![\w-]))(?=(?:[ .()+-]*\d){8})(?:\+\d{1,3}[ .-]?)?(?:\(?\d{2,4}\)?[ .-]){1,3}\d{2,4}(?![\w-]))"),
    ];

    public string Redact(string text, IEnumerable<RedactionPattern> patterns)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(patterns);

        var ordered = patterns
            .OrderBy(pattern => Rank(pattern) is var rank and >= 0 ? rank : int.MaxValue)
            .ToList();

        var result = text;
        foreach (var pattern in ordered)
        {
            try
            {
                result = new Regex(pattern.Expression, RegexOptions.CultureInvariant, Limit).Replace(result, Mask);
            }
            catch (Exception ex) when (ex is RegexMatchTimeoutException or ArgumentException)
            {
                return Mask;
            }
        }

        return result;
    }

    private int Rank(RedactionPattern pattern)
    {
        for (var index = 0; index < Patterns.Count; index++)
            if (Patterns[index].Expression == pattern.Expression) return index;
        return -1;
    }
}
