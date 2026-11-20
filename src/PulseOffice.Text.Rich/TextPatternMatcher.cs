using System.Text.RegularExpressions;

namespace PulseOffice.Text.Rich;

public sealed record TextPattern(string Text, bool MatchCase, bool WholeWords = false, bool Regex = false)
{
    public bool IsEmpty => string.IsNullOrEmpty(Text);
}

public readonly record struct TextPatternHit(int Start, int Length, string Replacement);

public interface ITextPatternMatcher
{
    IReadOnlyList<TextPatternHit> Find(TextPattern pattern, string source, string replacement = "");

    bool IsValid(TextPattern pattern);
}

public sealed class TextPatternMatcher : ITextPatternMatcher
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    public bool IsValid(TextPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        return !pattern.IsEmpty && Compile(pattern) is not null;
    }

    public IReadOnlyList<TextPatternHit> Find(TextPattern pattern, string source, string replacement = "")
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.IsEmpty || string.IsNullOrEmpty(source) || Compile(pattern) is not { } regex) return [];

        var hits = new List<TextPatternHit>();
        try
        {
            foreach (Match match in regex.Matches(source))
            {
                if (match.Length == 0) continue;
                var text = pattern.Regex ? match.Result(replacement ?? string.Empty) : replacement ?? string.Empty;
                hits.Add(new TextPatternHit(match.Index, match.Length, text));
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }
        return hits;
    }

    private Regex? Compile(TextPattern pattern)
    {
        var body = pattern.Regex ? pattern.Text : System.Text.RegularExpressions.Regex.Escape(pattern.Text);
        if (pattern.WholeWords) body = $@"(?<![\w]){(pattern.Regex ? "(?:" + body + ")" : body)}(?![\w])";
        var options = RegexOptions.CultureInvariant | (pattern.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        try
        {
            return new Regex(body, options, Timeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
