using System.Globalization;
using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Editing;

public sealed record SearchQuery(string Text, bool MatchCase = false, bool WholeWords = false);

public interface IDocumentSearch
{
    IReadOnlyList<DocumentRange> FindAll(IEnumerable<ParagraphBlock> paragraphs, SearchQuery query);

    DocumentRange? FindNext(IReadOnlyList<ParagraphBlock> paragraphs, SearchQuery query, DocumentPosition after);
}

public sealed class DocumentSearch : IDocumentSearch
{
    public IReadOnlyList<DocumentRange> FindAll(IEnumerable<ParagraphBlock> paragraphs, SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);
        ArgumentNullException.ThrowIfNull(query);
        if (query.Text.Length == 0) return [];
        var found = new List<DocumentRange>();
        foreach (var paragraph in paragraphs) found.AddRange(In(paragraph, query));
        return found;
    }

    public DocumentRange? FindNext(IReadOnlyList<ParagraphBlock> paragraphs, SearchQuery query, DocumentPosition after)
    {
        ArgumentNullException.ThrowIfNull(paragraphs);
        var all = FindAll(paragraphs, query);
        if (all.Count == 0) return null;
        var index = paragraphs.Select((paragraph, position) => (paragraph.Id, position)).ToDictionary(entry => entry.Id, entry => entry.position, StringComparer.Ordinal);
        var start = index.GetValueOrDefault(after.ParagraphId, -1);
        foreach (var range in all)
        {
            var at = index[range.Start.ParagraphId];
            if (at > start || at == start && range.Start.Offset >= after.Offset) return range;
        }
        return all[0];
    }

    private IEnumerable<DocumentRange> In(ParagraphBlock paragraph, SearchQuery query)
    {
        var compare = CultureInfo.CurrentCulture.CompareInfo;
        var options = query.MatchCase ? CompareOptions.Ordinal : CompareOptions.IgnoreCase;
        var text = paragraph.Text;
        var from = 0;
        while (from < text.Length)
        {
            var relative = compare.IndexOf(text.AsSpan(from), query.Text, options, out var length);
            if (relative < 0) yield break;
            var at = from + relative;
            var end = at + length;
            if (length > 0 && (!query.WholeWords || IsWordEdge(text, at - 1) && IsWordEdge(text, end)))
                yield return DocumentRange.In(paragraph.Id, at, end);
            from = Math.Max(end, at + 1);
        }
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
