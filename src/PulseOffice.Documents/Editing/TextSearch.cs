using System.Globalization;
using System.Text.RegularExpressions;
using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Editing;

public interface IDocumentSearch
{
    IReadOnlyList<DocumentRange> FindAll(IEnumerable<ParagraphBlock> paragraphs, SearchQuery query);

    IReadOnlyList<SearchMatch> Matches(IEnumerable<SearchStory> stories, SearchQuery query);

    SearchMatch? Next(IReadOnlyList<SearchMatch> matches, IReadOnlyList<SearchStory> stories, DocumentPosition from, bool backwards = false);

    string Replacement(SearchMatch match, SearchQuery query, string replacement);

    string Replacement(ParagraphBlock paragraph, DocumentRange found, SearchQuery query, string replacement);

    bool IsValid(SearchQuery query);
}

public sealed class DocumentSearch(ISearchTextBuilder builder, IWordWildcards wildcards, PulseOffice.Documents.Styles.IStyleResolver styles) : IDocumentSearch
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private const string AnyCharacter = "[^\\n\\f\\u000E\\u001D]";

    public DocumentSearch() : this(new SearchTextBuilder(), new WordWildcards(), new PulseOffice.Documents.Styles.StyleResolver())
    {
    }

    private readonly IReadOnlyDictionary<char, string> _literals = new Dictionary<char, string>
    {
        ['t'] = "\t",
        ['l'] = "\u2028",
        ['s'] = " ",
        ['~'] = "‑",
        ['-'] = "­",
        ['^'] = "\\^",
        ['p'] = "\\n",
        ['m'] = "\\f",
        ['n'] = "\\u000E",
        ['b'] = "\\u001D",
        ['?'] = AnyCharacter,
        ['#'] = "[0-9]",
        ['$'] = "\\p{L}",
        ['w'] = "[ \\t\\u00A0]+",
    };

    private readonly IReadOnlyDictionary<char, string> _replacements = new Dictionary<char, string>
    {
        ['t'] = "\t",
        ['l'] = "\u2028",
        ['s'] = " ",
        ['~'] = "‑",
        ['-'] = "­",
        ['^'] = "^",
        ['p'] = "\n",
        ['m'] = "\f",
        ['n'] = "\u000E",
        ['b'] = "\u001D",
    };

    public IReadOnlyList<DocumentRange> FindAll(IEnumerable<ParagraphBlock> paragraphs, SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);
        return [.. Matches([new SearchStory([.. paragraphs])], query).Select(match => match.Range)];
    }

    public IReadOnlyList<SearchMatch> Matches(IEnumerable<SearchStory> stories, SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(stories);
        ArgumentNullException.ThrowIfNull(query);
        if (query.IsEmpty) return [];
        Regex? pattern = null;
        var literal = query.Text.Length > 0 && !query.Regex && !query.Wildcards && !NeedsPattern(query.Text);
        if (query.Text.Length > 0 && !literal && (pattern = Pattern(query)) is null) return [];
        var found = new List<SearchMatch>();
        foreach (var story in stories)
        {
            var text = builder.Build(story);
            var spans = query.Text.Length == 0 ? FormatSpans(text, query.Format!)
                : literal ? Literally(text.Text, query, Carets(query.Text, "^&"))
                : Patterned(text.Text, pattern!);
            foreach (var (start, end, match) in spans)
            {
                if (end <= start) continue;
                if (query.WholeWords && !query.Wildcards && !(IsWordEdge(text.Text, start - 1) && IsWordEdge(text.Text, end))) continue;
                if (query.HasFormat && !Fits(text, start, end, query.Format!)) continue;
                found.Add(text.Match(start, end, match));
            }
        }
        return found;
    }

    public SearchMatch? Next(IReadOnlyList<SearchMatch> matches, IReadOnlyList<SearchStory> stories, DocumentPosition from, bool backwards = false)
    {
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentNullException.ThrowIfNull(stories);
        if (matches.Count == 0) return null;
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var paragraph in stories.SelectMany(story => story.Paragraphs)) index.TryAdd(paragraph.Id, index.Count);
        var at = index.GetValueOrDefault(from.ParagraphId, -1);
        int Order(DocumentPosition position) => index.GetValueOrDefault(position.ParagraphId, -1);
        if (backwards)
        {
            return matches.LastOrDefault(match => Order(match.Range.Start) < at || Order(match.Range.Start) == at && match.Range.Start.Offset < from.Offset)
                ?? matches[^1];
        }
        return matches.FirstOrDefault(match => Order(match.Range.Start) > at || Order(match.Range.Start) == at && match.Range.Start.Offset >= from.Offset)
            ?? matches[0];
    }

    public bool IsValid(SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.Text.Length == 0 || Pattern(query) is not null;
    }

    public string Replacement(SearchMatch match, SearchQuery query, string replacement)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(replacement);
        if (query.Wildcards && match.Pattern is { } wild) return wild.Result(Carets(wildcards.Replacement(replacement), "$0"));
        if (query.Regex && match.Pattern is { } regex) return regex.Result(Carets(replacement, "$0"));
        return Carets(replacement, match.Found);
    }

    public string Replacement(ParagraphBlock paragraph, DocumentRange found, SearchQuery query, string replacement)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        var match = Matches([new SearchStory([paragraph])], query).FirstOrDefault(candidate => candidate.Range == found)
            ?? new SearchMatch(found, paragraph.Text[Math.Clamp(found.Start.Offset, 0, paragraph.Length)..Math.Clamp(found.End.Offset, 0, paragraph.Length)]);
        return Replacement(match, query, replacement);
    }

    private string Carets(string replacement, string whole)
    {
        var built = new System.Text.StringBuilder(replacement.Length);
        for (var i = 0; i < replacement.Length; i++)
        {
            if (replacement[i] == '^' && i + 1 < replacement.Length)
            {
                var code = replacement[i + 1];
                if (code == '&' || _replacements.ContainsKey(code))
                {
                    built.Append(code == '&' ? whole : _replacements[code]);
                    i++;
                    continue;
                }
            }
            built.Append(replacement[i]);
        }
        return built.ToString();
    }

    private Regex? Pattern(SearchQuery query)
    {
        var source = query.Wildcards ? wildcards.Pattern(query.Text) : query.Regex ? query.Text : Plain(query.Text);
        if (source is null) return null;
        var options = RegexOptions.CultureInvariant
            | (query.Regex && !query.Wildcards ? RegexOptions.Multiline : RegexOptions.None)
            | (query.MatchCase || query.Wildcards ? RegexOptions.None : RegexOptions.IgnoreCase);
        try
        {
            return new Regex(source, options, MatchTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private string Plain(string text)
    {
        var built = new System.Text.StringBuilder(text.Length * 2);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '^' && i + 1 < text.Length && _literals.TryGetValue(text[i + 1], out var token))
            {
                built.Append(token);
                i++;
                continue;
            }
            built.Append(Regex.Escape(text[i].ToString()));
        }
        return built.ToString();
    }

    private bool NeedsPattern(string text)
    {
        for (var i = 0; i + 1 < text.Length; i++)
        {
            if (text[i] != '^') continue;
            if (text[i + 1] is '?' or '#' or '$' or 'w' or 'p' or 'm' or 'n' or 'b') return true;
            i++;
        }
        return false;
    }

    private IEnumerable<(int Start, int End, Match? Match)> Literally(string text, SearchQuery query, string literal)
    {
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        var options = query.MatchCase ? CompareOptions.Ordinal : CompareOptions.IgnoreCase;
        var from = 0;
        while (from < text.Length)
        {
            var relative = compare.IndexOf(text.AsSpan(from), literal, options, out var length);
            if (relative < 0) yield break;
            var at = from + relative;
            var end = at + length;
            if (length > 0 && text.AsSpan(at, length).IndexOfAny(Breaks) < 0) yield return (at, end, null);
            from = Math.Max(end, at + 1);
        }
    }

    private static readonly System.Buffers.SearchValues<char> Breaks =
        System.Buffers.SearchValues.Create([SearchMarks.Paragraph, SearchMarks.Page, SearchMarks.Column, SearchMarks.Section]);

    private IEnumerable<(int Start, int End, Match? Match)> Patterned(string text, Regex pattern)
    {
        var found = new List<(int, int, Match?)>();
        try
        {
            foreach (Match match in pattern.Matches(text))
            {
                if (match.Length > 0) found.Add((match.Index, match.Index + match.Length, match));
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }
        return found;
    }

    private IEnumerable<(int Start, int End, Match? Match)> FormatSpans(SearchText text, SearchFormat format)
    {
        var start = -1;
        for (var i = 0; i <= text.Text.Length; i++)
        {
            var fits = i < text.Text.Length && !text.IsMark(i) && Fits(text, i, i + 1, format);
            if (fits && start < 0) start = i;
            if (fits || start < 0) continue;
            yield return (start, i, null);
            start = -1;
        }
    }

    private bool Fits(SearchText text, int start, int end, SearchFormat format)
    {
        for (var i = start; i < end; i++)
        {
            if (text.ParagraphAt(i) is not { } paragraph) continue;
            if (format.ParagraphStyle is { } style && !string.Equals(paragraph.StyleName, style, StringComparison.Ordinal)) return false;
            var run = text.RunAt(i);
            if (format.CharacterStyle is { } character && !string.Equals(run?.StyleName, character, StringComparison.Ordinal)) return false;
            if (!format.HasCharacterFormat) continue;
            var effective = Effective(text, paragraph, run);
            if (format.Bold is { } bold && effective.Bold != bold) return false;
            if (format.Italic is { } italic && effective.Italic != italic) return false;
            if (format.Underline is { } underline && effective.Underline != underline) return false;
        }
        return true;
    }

    private (bool Bold, bool Italic, bool Underline) Effective(SearchText text, ParagraphBlock paragraph, PulseOffice.Text.Rich.TextRun? run)
    {
        if (text.Styles is not { } sheet)
            return (run?.Bold ?? paragraph.Content.Bold, run?.Italic ?? paragraph.Content.Italic, run?.Underline ?? paragraph.Content.Underline);
        if (run is null)
        {
            var whole = styles.EffectiveParagraph(sheet, paragraph.Content);
            return (whole.Bold, whole.Italic, whole.Underline);
        }
        var resolved = styles.EffectiveRun(sheet, paragraph.Content, run);
        return (resolved.Bold, resolved.Italic, resolved.Underline);
    }

    private bool IsWordEdge(string text, int index) =>
        index < 0 || index >= text.Length || !char.IsLetterOrDigit(text[index]) && text[index] != '_';
}

public sealed record DocumentStatistics(int Words, int Characters, int CharactersWithoutSpaces, int Paragraphs);

public interface IDocumentStatisticsCounter
{
    DocumentStatistics Count(IEnumerable<string> paragraphs);
}

public sealed class DocumentStatisticsCounter : IDocumentStatisticsCounter
{
    public DocumentStatistics Count(IEnumerable<string> paragraphs)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);
        int words = 0, characters = 0, visible = 0, count = 0;
        foreach (var paragraph in paragraphs)
        {
            if (paragraph.Length > 0) count++;
            var inWord = false;
            var elements = StringInfo.GetTextElementEnumerator(paragraph);
            while (elements.MoveNext())
            {
                var element = (string)elements.Current;
                characters++;
                var space = element.Length == 1 && (char.IsWhiteSpace(element[0]) || element[0] == '\u2028');
                if (!space) visible++;
                if (space) inWord = false;
                else if (!inWord)
                {
                    inWord = true;
                    words++;
                }
            }
        }
        return new DocumentStatistics(words, characters, visible, count);
    }
}
