using System.Text;

namespace PulseOffice.Foundation.Pages;

public enum HeaderFooterTokenKind
{
    Text,
    Field,
    Formatting,
}

public readonly record struct HeaderFooterToken(HeaderFooterTokenKind Kind, string Value);

public interface IHeaderFooterCodes
{
    IReadOnlyList<HeaderFooterToken> Tokens(string? pattern);
}

public sealed class HeaderFooterCodes : IHeaderFooterCodes
{
    private const string Fields = "PNDTFAZ";
    private const string Switches = "BIUESXYOHG";
    private const int ColorLength = 6;

    public IReadOnlyList<HeaderFooterToken> Tokens(string? pattern)
    {
        var tokens = new List<HeaderFooterToken>();
        if (string.IsNullOrEmpty(pattern)) return tokens;

        var literal = new StringBuilder();
        var index = 0;

        while (index < pattern.Length)
        {
            if (pattern[index] != '&' || index == pattern.Length - 1)
            {
                literal.Append(pattern[index++]);
                continue;
            }

            var code = pattern[index + 1];
            var upper = char.ToUpperInvariant(code);

            if (code == '&')
            {
                literal.Append('&');
                index += 2;
                continue;
            }

            var end = upper switch
            {
                _ when Fields.Contains(upper) => index + 2,
                _ when Switches.Contains(upper) => index + 2,
                'K' => Math.Min(pattern.Length, index + 2 + ColorLength),
                '"' => Quoted(pattern, index + 2),
                _ when char.IsAsciiDigit(code) => Digits(pattern, index + 1),
                _ => -1,
            };

            if (end < 0)
            {
                literal.Append(pattern, index, 2);
                index += 2;
                continue;
            }

            Flush(tokens, literal);
            tokens.Add(Fields.Contains(upper)
                ? new HeaderFooterToken(HeaderFooterTokenKind.Field, upper.ToString())
                : new HeaderFooterToken(HeaderFooterTokenKind.Formatting, pattern[index..end]));
            index = end;
        }

        Flush(tokens, literal);
        return tokens;
    }

    private int Quoted(string pattern, int start)
    {
        var close = pattern.IndexOf('"', start);
        return close < 0 ? pattern.Length : close + 1;
    }

    private int Digits(string pattern, int start)
    {
        var end = start;
        while (end < pattern.Length && char.IsAsciiDigit(pattern[end])) end++;
        return end;
    }

    private void Flush(List<HeaderFooterToken> tokens, StringBuilder literal)
    {
        if (literal.Length == 0) return;
        tokens.Add(new HeaderFooterToken(HeaderFooterTokenKind.Text, literal.ToString()));
        literal.Clear();
    }
}
