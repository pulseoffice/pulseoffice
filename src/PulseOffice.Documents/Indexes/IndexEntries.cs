using System.Buffers;
using System.Globalization;
using System.Text;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Indexes;

public sealed record IndexEntry(string Text, int Level, IReadOnlyList<string> ParagraphIds)
{
    public string? Link { get; init; }

    public string? Prefix { get; init; }
}

public interface IIndexEntrySource
{
    IReadOnlyCollection<IndexKind> Kinds { get; }

    IReadOnlyList<IndexEntry> Collect(TextDocument document, IndexDefinition definition);
}

public interface IIndexScope
{
    IEnumerable<ParagraphBlock> Paragraphs(TextDocument document);
}

public sealed class IndexScope(IDocumentReadingOrder order, IBlockTree tree) : IIndexScope
{
    public IEnumerable<ParagraphBlock> Paragraphs(TextDocument document)
    {
        var generated = tree.Blocks(document.Body).OfType<SectionBlock>().Where(section => section.IsIndex)
            .SelectMany(section => tree.Paragraphs(section.Content)).Select(paragraph => paragraph.Id).ToHashSet(StringComparer.Ordinal);
        return order.BodyWithFrames(document).Where(paragraph => !generated.Contains(paragraph.Id));
    }
}

public sealed class ContentsEntrySource(IIndexScope scope, IDocumentOutline outline) : IIndexEntrySource
{
    public IReadOnlyCollection<IndexKind> Kinds { get; } = [IndexKind.Contents];

    public IReadOnlyList<IndexEntry> Collect(TextDocument document, IndexDefinition definition)
    {
        var entries = new List<IndexEntry>();
        foreach (var paragraph in scope.Paragraphs(document))
        {
            if (definition.FromOutline && outline.LevelOf(document, paragraph) is { } level && level <= definition.Levels && paragraph.Text.Trim() is { Length: > 0 } text)
                entries.Add(new IndexEntry(text, level, [paragraph.Id]) { Link = "#" + text + "|outline" });
            if (!definition.FromMarks) continue;
            foreach (var mark in paragraph.Marks.OfType<IndexEntryMark>().Where(mark => mark.Index is "toc" && mark.Edge != MarkEdge.End))
                entries.Add(new IndexEntry(mark.Entry, 1, [paragraph.Id]));
        }
        return entries;
    }
}

public sealed class AlphabeticalEntrySource(IIndexScope scope, ITextCultures cultures) : IIndexEntrySource
{
    public AlphabeticalEntrySource(IIndexScope scope) : this(scope, new TextCultures())
    {
    }

    public IReadOnlyCollection<IndexKind> Kinds { get; } = [IndexKind.Alphabetical, IndexKind.User];

    public IReadOnlyList<IndexEntry> Collect(TextDocument document, IndexDefinition definition)
    {
        var index = definition.Kind == IndexKind.User ? definition.UserName ?? "user" : "alphabetical";
        var collected = new List<(IReadOnlyList<string> Path, string ParagraphId)>();
        foreach (var paragraph in scope.Paragraphs(document))
        {
            foreach (var mark in paragraph.Marks.OfType<IndexEntryMark>().Where(mark => mark.Index == index && mark.Edge != MarkEdge.End))
                collected.Add((mark.Path, paragraph.Id));
        }
        if (definition.Kind == IndexKind.User && definition.SortByPosition)
            return [.. collected.Select(entry => new IndexEntry(string.Join(", ", entry.Path), 1, [entry.ParagraphId]))];
        var culture = cultures.For(document.Language);
        var comparer = StringComparer.Create(culture, definition.CaseSensitive ? CompareOptions.None : CompareOptions.IgnoreCase);
        var paths = new PathComparer(comparer);
        var groups = definition.CombineEntries
            ? collected.GroupBy(entry => entry.Path, paths).Select(group => (group.Key, (IReadOnlyList<string>)[.. group.Select(entry => entry.ParagraphId)]))
            : collected.Select(entry => (entry.Path, (IReadOnlyList<string>)[entry.ParagraphId]));
        var sorted = groups.OrderBy(entry => entry.Item1, paths).ToList();
        var entries = new List<IndexEntry>();
        IReadOnlyList<string> previous = [];
        string? letter = null;
        foreach (var (path, paragraphs) in sorted)
        {
            var initial = Initial(path[0], culture);
            if (definition.LetterSeparators && initial != letter)
            {
                letter = initial;
                entries.Add(new IndexEntry(initial, 0, []));
            }
            var shared = 0;
            while (shared < previous.Count && shared < path.Count - 1 && comparer.Equals(previous[shared], path[shared])) shared++;
            for (var level = shared; level < path.Count - 1; level++) entries.Add(new IndexEntry(path[level], level + 1, []));
            entries.Add(new IndexEntry(path[^1], path.Count, paragraphs));
            previous = path;
        }
        return entries;
    }

    private string Initial(string text, CultureInfo culture)
    {
        if (text.Length == 0) return string.Empty;
        return Rune.DecodeFromUtf16(text, out var first, out var length) == OperationStatus.Done
            ? Rune.ToUpper(first, culture).ToString()
            : text[..length];
    }

    private sealed class PathComparer(StringComparer text) : IComparer<IReadOnlyList<string>>, IEqualityComparer<IReadOnlyList<string>>
    {
        public int Compare(IReadOnlyList<string>? x, IReadOnlyList<string>? y)
        {
            x ??= [];
            y ??= [];
            for (var index = 0; index < Math.Min(x.Count, y.Count); index++)
            {
                var order = text.Compare(x[index], y[index]);
                if (order != 0) return order;
            }
            return x.Count.CompareTo(y.Count);
        }

        public bool Equals(IReadOnlyList<string>? x, IReadOnlyList<string>? y) => Compare(x, y) == 0;

        public int GetHashCode(IReadOnlyList<string> obj) => obj.Aggregate(obj.Count, (hash, part) => HashCode.Combine(hash, text.GetHashCode(part)));
    }
}

public sealed class CaptionEntrySource(IIndexScope scope) : IIndexEntrySource
{
    public IReadOnlyCollection<IndexKind> Kinds { get; } = [IndexKind.Illustrations, IndexKind.Tables];

    public IReadOnlyList<IndexEntry> Collect(TextDocument document, IndexDefinition definition)
    {
        var sequence = definition.Sequence ?? (definition.Kind == IndexKind.Tables ? "Table" : "Figure");
        var entries = new List<IndexEntry>();
        foreach (var paragraph in scope.Paragraphs(document))
        {
            var offset = 0;
            foreach (var run in paragraph.Content.Runs)
            {
                offset += run.Text.Length;
                if (run.Field != TextFieldKind.Sequence || !string.Equals(run.FieldArgument, sequence, StringComparison.OrdinalIgnoreCase)) continue;
                var text = definition.Captions switch
                {
                    CaptionDisplay.CategoryAndNumber => paragraph.Text[..offset],
                    CaptionDisplay.Text => paragraph.Text[offset..].TrimStart(' ', ':', '.', '-', '–').Trim(),
                    _ => paragraph.Text.Trim(),
                };
                entries.Add(new IndexEntry(text, 1, [paragraph.Id]));
                break;
            }
        }
        return entries;
    }
}

public sealed class BibliographyEntrySource(IIndexScope scope, ITextCultures cultures) : IIndexEntrySource
{
    public BibliographyEntrySource(IIndexScope scope) : this(scope, new TextCultures())
    {
    }

    public IReadOnlyCollection<IndexKind> Kinds { get; } = [IndexKind.Bibliography];

    public IReadOnlyList<IndexEntry> Collect(TextDocument document, IndexDefinition definition)
    {
        var cited = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var paragraph in scope.Paragraphs(document))
            foreach (var run in paragraph.Content.Runs.Where(run => run.Field == TextFieldKind.Citation && run.FieldArgument is not null))
                if (seen.Add(run.FieldArgument!)) cited.Add(run.FieldArgument!);
        var sources = cited.Select(identifier => document.Bibliography.Find(identifier) ?? new BibliographySource(identifier, "misc", new Dictionary<string, string>()));
        if (!definition.SortByPosition) sources = sources.OrderBy(source => source.Value("author"), StringComparer.Create(cultures.For(document.Language), ignoreCase: true)).ThenBy(source => source.Identifier, StringComparer.Ordinal);
        return [.. sources.Select(source => new IndexEntry(Describe(source), 1, []) { Prefix = "[" + source.Identifier + "]" })];
    }

    private string Describe(BibliographySource source) =>
        string.Join(", ", new[] { source.Value("author"), source.Value("title"), source.Value("publisher"), source.Value("year") }.Where(part => part.Length > 0));
}
