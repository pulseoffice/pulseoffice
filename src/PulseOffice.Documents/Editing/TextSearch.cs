using System.Globalization;
using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Editing;

public sealed record SearchQuery(string Text, bool MatchCase = false, bool WholeWords = false, bool Regex = false);

public interface IDocumentSearch
{
    IReadOnlyList<DocumentRange> FindAll(IEnumerable<ParagraphBlock> paragraphs, SearchQuery query);

    DocumentRange? FindNext(IReadOnlyList<ParagraphBlock> paragraphs, SearchQuery query, DocumentPosition after);

    string Replacement(ParagraphBlock paragraph, DocumentRange found, SearchQuery query, string replacement);

    bool IsValid(SearchQuery query);
}

public sealed class DocumentSearch : IDocumentSearch
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private readonly IReadOnlyDictionary<char, string> _literals = new Dictionary<char, string>
    {
        ['t'] = "\t",
        ['l'] = "\u2028",
        ['s'] = "\u00A0",
        ['~'] = "\u2011",
        ['-'] = "\u00AD",
        ['^'] = "\\^",
        ['?'] = ".",
        ['#'] = "[0-9]",
        ['$'] = "\\p{L}",
        ['w'] = "[ \\t\\u00A0]+",
    };

    private readonly IReadOnlyDictionary<char, string> _replacements = new Dictionary<char, string>
    {
        ['t'] = "\t",
        ['l'] = "\u2028",
        ['s'] = "\u00A0",
        ['~'] = "\u2011",
        ['-'] = "\u00AD",
        ['^'] = "^",
    };

    public IReadOnlyList<DocumentRange> FindAll(IEnumerable<ParagraphBlock> paragraphs, SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);
        ArgumentNullException.ThrowIfNull(query);
        if (query.Text.Length == 0) return [];
        var found = new List<DocumentRange>();
        if (!query.Regex && !HasWildcards(query.Text))
        {
            var literal = Carets(query.Text, "^&");
            foreach (var paragraph in paragraphs) found.AddRange(Literally(paragraph, query, literal));
            return found;
        }
        if (Pattern(query) is not { } pattern) return [];
        foreach (var paragraph in paragraphs) found.AddRange(In(paragraph, query, pattern));
        return found;
    }

    public DocumentRange? FindNext(IReadOnlyList<ParagraphBlock> paragraphs, SearchQuery query, DocumentPosition after)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);
        var all = FindAll(paragraphs, query);
        if (all.Count == 0) return null;
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var position = 0; position < paragraphs.Count; position++) index.TryAdd(paragraphs[position].Id, position);
        var start = index.GetValueOrDefault(after.ParagraphId, -1);
        foreach (var range in all)
        {
            var at = index[range.Start.ParagraphId];
            if (at > start || at == start && range.Start.Offset >= after.Offset) return range;
        }
        return all[0];
    }

    public bool IsValid(SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.Text.Length == 0 || Pattern(query) is not null;
    }

    public string Replacement(ParagraphBlock paragraph, DocumentRange found, SearchQuery query, string replacement)
    {
        ArgumentNullException.ThrowIfNull(paragraph);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(replacement);
        var text = paragraph.Text;
        var start = Math.Clamp(found.Start.Offset, 0, text.Length);
        var end = Math.Clamp(found.End.Offset, start, text.Length);
        if (query.Regex && Pattern(query) is { } pattern && pattern.Match(text, start) is { Success: true } match && match.Index == start && match.Length == end - start)
            return match.Result(Carets(replacement, "$0"));
        return Carets(replacement, text[start..end]);
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

    private System.Text.RegularExpressions.Regex? Pattern(SearchQuery query)
    {
        var options = System.Text.RegularExpressions.RegexOptions.CultureInvariant
            | (query.MatchCase ? System.Text.RegularExpressions.RegexOptions.None : System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var source = query.Regex ? query.Text : Plain(query.Text);
        try
        {
            return new System.Text.RegularExpressions.Regex(source, options, MatchTimeout);
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
            built.Append(System.Text.RegularExpressions.Regex.Escape(text[i].ToString()));
        }
        return built.ToString();
    }

    private bool HasWildcards(string text)
    {
        for (var i = 0; i + 1 < text.Length; i++)
        {
            if (text[i] != '^') continue;
            if (text[i + 1] is '?' or '#' or '$' or 'w') return true;
            i++;
        }
        return false;
    }

    private IEnumerable<DocumentRange> Literally(ParagraphBlock paragraph, SearchQuery query, string literal)
    {
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        var options = query.MatchCase ? CompareOptions.Ordinal : CompareOptions.IgnoreCase;
        var text = paragraph.Text;
        var from = 0;
        while (from < text.Length)
        {
            var relative = compare.IndexOf(text.AsSpan(from), literal, options, out var length);
            if (relative < 0) yield break;
            var at = from + relative;
            var end = at + length;
            if (length > 0 && (!query.WholeWords || IsWordEdge(text, at - 1) && IsWordEdge(text, end)))
                yield return DocumentRange.In(paragraph.Id, at, end);
            from = Math.Max(end, at + 1);
        }
    }

    private IEnumerable<DocumentRange> In(ParagraphBlock paragraph, SearchQuery query, System.Text.RegularExpressions.Regex pattern)
    {
        var text = paragraph.Text;
        var matches = new List<DocumentRange>();
        try
        {
            foreach (System.Text.RegularExpressions.Match match in pattern.Matches(text))
            {
                if (match.Length == 0) continue;
                var end = match.Index + match.Length;
                if (!query.WholeWords || IsWordEdge(text, match.Index - 1) && IsWordEdge(text, end))
                    matches.Add(DocumentRange.In(paragraph.Id, match.Index, end));
            }
        }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
        {
            return [];
        }
        return matches;
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
