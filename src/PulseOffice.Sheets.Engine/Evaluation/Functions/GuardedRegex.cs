using System.Text;
using System.Text.RegularExpressions;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public interface IGuardedRegex
{
    Regex? Compile(string pattern, bool ignoreCase);

    IReadOnlyList<Match>? Matches(Regex regex, string text);

    string? Replace(Regex regex, string text, string replacement, Func<int, int, bool> chosen);
}

public sealed class GuardedRegex : IGuardedRegex
{
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(1);

    private const int MaximumMatches = ResultLimits.MaximumTextLength + 1;

    public Regex? Compile(string pattern, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var options = RegexOptions.CultureInvariant;
        if (ignoreCase) options |= RegexOptions.IgnoreCase;
        try
        {
            return new Regex(pattern, options, _timeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public IReadOnlyList<Match>? Matches(Regex regex, string text)
    {
        ArgumentNullException.ThrowIfNull(regex);
        ArgumentNullException.ThrowIfNull(text);
        var found = new List<Match>();
        try
        {
            for (var match = regex.Match(text); match.Success; match = match.NextMatch())
            {
                if (found.Count == MaximumMatches) return null;
                found.Add(match);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
        return found;
    }

    public string? Replace(Regex regex, string text, string replacement, Func<int, int, bool> chosen)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(chosen);
        if (Matches(regex, text) is not { } matches) return null;
        var builder = new StringBuilder();
        var copied = 0;
        try
        {
            for (var index = 0; index < matches.Count; index++)
            {
                if (!chosen(index, matches.Count)) continue;
                var match = matches[index];
                builder.Append(text, copied, match.Index - copied).Append(match.Result(replacement));
                copied = match.Index + match.Length;
                if (!ResultLimits.TextFits((long)builder.Length + text.Length - copied)) return null;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
        builder.Append(text, copied, text.Length - copied);
        return builder.ToString();
    }
}
