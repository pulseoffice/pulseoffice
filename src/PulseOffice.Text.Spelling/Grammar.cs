using System.Text.RegularExpressions;

namespace PulseOffice.Text.Spelling;

public sealed record GrammarIssue(WordSpan Span, string Rule, IReadOnlyList<string> Suggestions);

public interface IGrammarRule
{
    IEnumerable<GrammarIssue> Find(string text, LanguageTag language);
}

public interface IGrammarChecker
{
    IReadOnlyList<GrammarIssue> Check(string text, LanguageTag language);
}

public sealed class GrammarChecker(IEnumerable<IGrammarRule> rules) : IGrammarChecker
{
    public IReadOnlyList<GrammarIssue> Check(string text, LanguageTag language)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        return [.. rules.SelectMany(rule => rule.Find(text, language)).OrderBy(issue => issue.Span.Start)];
    }
}

public sealed partial class RepeatedWordRule : IGrammarRule
{
    public IEnumerable<GrammarIssue> Find(string text, LanguageTag language)
    {
        foreach (Match match in Repeated().Matches(text))
        {
            var word = match.Groups["word"].Value;
            if (word.All(char.IsDigit)) continue;
            yield return new GrammarIssue(new WordSpan(match.Index, match.Length, match.Value), "repeatedWord", [word]);
        }
    }

    [GeneratedRegex(@"\b(?<word>\w+)(?:\s+\k<word>)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private partial Regex Repeated();
}

public sealed partial class DoubleSpaceRule : IGrammarRule
{
    public IEnumerable<GrammarIssue> Find(string text, LanguageTag language)
    {
        foreach (Match match in Spaces().Matches(text))
        {
            if (match.Index == 0 || match.Index + match.Length >= text.Length) continue;
            yield return new GrammarIssue(new WordSpan(match.Index, match.Length, match.Value), "doubleSpace", [" "]);
        }
    }

    [GeneratedRegex(@"(?<=\S) {2,}(?=\S)", RegexOptions.CultureInvariant)]
    private partial Regex Spaces();
}

public sealed partial class SpaceBeforePunctuationRule : IGrammarRule
{
    public IEnumerable<GrammarIssue> Find(string text, LanguageTag language)
    {
        var french = language.Language == "fr";
        foreach (Match match in Before().Matches(text))
        {
            var mark = match.Groups["mark"].Value;
            if (french && mark is ";" or ":" or "!" or "?") continue;
            yield return new GrammarIssue(new WordSpan(match.Index, match.Length, match.Value), "spaceBeforePunctuation", [mark]);
        }
    }

    [GeneratedRegex(@"(?<=\w) +(?<mark>[,.;:!?])(?![.\w])", RegexOptions.CultureInvariant)]
    private partial Regex Before();
}

public sealed partial class SentenceStartRule : IGrammarRule
{
    public IEnumerable<GrammarIssue> Find(string text, LanguageTag language)
    {
        foreach (Match match in Start().Matches(text))
        {
            var word = match.Groups["word"];
            if (!char.IsLower(word.Value[0]) || word.Value.Length < 2 || Abbreviation(text, match.Index)) continue;
            var fixedWord = char.ToUpper(word.Value[0], System.Globalization.CultureInfo.InvariantCulture) + word.Value[1..];
            yield return new GrammarIssue(new WordSpan(word.Index, word.Length, word.Value), "sentenceCapital", [fixedWord]);
        }
    }

    private bool Abbreviation(string text, int dot)
    {
        var start = dot;
        while (start > 0 && char.IsLetter(text[start - 1])) start--;
        return dot - start is > 0 and <= 3 && dot >= 1 && text[dot] == '.' && char.IsLower(text[start]);
    }

    [GeneratedRegex(@"[.!?]\s+(?<word>\p{L}+)", RegexOptions.CultureInvariant)]
    private partial Regex Start();
}
