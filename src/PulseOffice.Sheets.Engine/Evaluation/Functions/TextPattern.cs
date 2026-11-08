using System.Text.RegularExpressions;

namespace PulseOffice.Sheets.Engine.Evaluation.Functions;

public abstract class TextPattern
{
    private readonly Regex _regex;

    protected TextPattern(string expression, bool caseSensitive, bool dotMatchesNewline = true, bool linearTime = false)
    {
        var options = RegexOptions.CultureInvariant | (dotMatchesNewline ? RegexOptions.Singleline : RegexOptions.None) | (caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase)
            | (linearTime ? RegexOptions.NonBacktracking : RegexOptions.None);
        _regex = TryCreate(expression, options) ?? new Regex("(?!)", options & ~RegexOptions.NonBacktracking, TimeSpan.FromSeconds(1));
    }

    public abstract bool HasMetacharacters { get; }

    public bool IsMatch(string text)
    {
        try { return _regex.IsMatch(text); }
        catch (RegexMatchTimeoutException) { return false; }
    }

    public int IndexOf(string text, int start)
    {
        try
        {
            var match = _regex.Match(text, start);
            return match.Success ? match.Index : -1;
        }
        catch (RegexMatchTimeoutException) { return -1; }
    }

    private Regex? TryCreate(string expression, RegexOptions options)
    {
        try { return new Regex(expression, options, TimeSpan.FromSeconds(1)); }
        catch (ArgumentException) { return null; }
    }
}

public sealed class RegularExpressionPattern(string expression, bool anchored, bool caseSensitive)
    : TextPattern(anchored ? $"\\A(?:{expression})\\z" : expression, caseSensitive, dotMatchesNewline: false)
{
    public override bool HasMetacharacters { get; } = expression.IndexOfAny(['.', '[', ']', '(', ')', '*', '+', '?', '{', '}', '|', '^', '$', '\\']) >= 0;
}

public sealed class TextRules(CalculationSettings settings)
{
    private TextRules? _forQueries;

    public CalculationSettings Settings => settings;

    public TextRules ForQueries => _forQueries ??= settings.CaseSensitive ? new TextRules(settings with { CaseSensitive = false }) : this;

    public StringComparison Comparison => settings.CaseSensitive ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;

    public TextPattern? Pattern(string text, bool anchored)
    {
        var whole = anchored && settings.WholeCellCriteria;
        return Pattern(text, whole, settings.CaseSensitive)
            ?? (anchored && !whole ? new RegularExpressionPattern(System.Text.RegularExpressions.Regex.Escape(text), anchored: false, settings.CaseSensitive) : null);
    }

    public TextPattern? SearchPattern(string text) => Pattern(text, anchored: false, caseSensitive: false);

    private TextPattern? Pattern(string text, bool anchored, bool caseSensitive)
    {
        TextPattern? pattern = settings.Criteria switch
        {
            CriteriaSyntax.Wildcards => new WildcardPattern(text, anchored, caseSensitive),
            CriteriaSyntax.RegularExpressions => new RegularExpressionPattern(text, anchored, caseSensitive),
            _ => null,
        };
        return pattern is { HasMetacharacters: true } ? pattern : null;
    }
}
