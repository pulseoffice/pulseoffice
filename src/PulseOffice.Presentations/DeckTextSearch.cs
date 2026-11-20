using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public sealed class DeckTextSearch(ISlideTextSources sources, ITextPatternMatcher matcher)
{
    public DeckTextSearch() : this(new SlideTextSources(), new TextPatternMatcher())
    {
    }

    public int Count(RichText text, string query, bool matchCase) => Count(text, new TextPattern(query, matchCase));

    public int Count(RichText text, TextPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (pattern.IsEmpty) return 0;
        return text.Paragraphs.Sum(paragraph => matcher.Find(pattern, Joined(paragraph)).Count);
    }

    public IReadOnlyList<DeckTextMatch> Matches(IReadOnlyList<Slide> slides, string query, bool matchCase) =>
        Matches(slides, new TextPattern(query, matchCase));

    public IReadOnlyList<DeckTextMatch> Matches(IReadOnlyList<Slide> slides, TextPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(slides);
        var found = new List<DeckTextMatch>();
        if (pattern.IsEmpty) return found;

        for (var slideIndex = 0; slideIndex < slides.Count; slideIndex++)
        {
            var slide = slides[slideIndex];
            foreach (var element in slide.Elements.OrderBy(e => e.ZOrder))
                foreach (var source in sources.Of(element))
                    for (var paragraph = 0; paragraph < source.Text.Paragraphs.Count; paragraph++)
                        foreach (var hit in matcher.Find(pattern, Joined(source.Text.Paragraphs[paragraph])))
                            found.Add(new DeckTextMatch(slideIndex, element.Id, paragraph, hit.Start)
                            {
                                Row = source.Row,
                                Column = source.Column,
                                Length = hit.Length,
                            });

            foreach (var hit in matcher.Find(pattern, slide.Notes ?? string.Empty))
                found.Add(new DeckTextMatch(slideIndex, null, 0, hit.Start) { Length = hit.Length });
        }

        return found;
    }

    public DeckTextMatch? Next(IReadOnlyList<DeckTextMatch> matches, DeckTextMatch? previous, int currentSlide)
    {
        if (matches.Count == 0) return null;
        if (previous is not null)
        {
            var at = -1;
            for (var i = 0; i < matches.Count; i++)
                if (matches[i] == previous) { at = i; break; }
            if (at >= 0) return matches[(at + 1) % matches.Count];
        }

        return matches.FirstOrDefault(match => match.SlideIndex >= currentSlide) ?? matches[0];
    }

    public bool SlideMatches(Slide slide, string query, bool matchCase) => SlideMatches(slide, new TextPattern(query, matchCase));

    public bool SlideMatches(Slide slide, TextPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(slide);
        return slide.Elements.SelectMany(sources.Of).Any(source => Count(source.Text, pattern) > 0)
            || matcher.Find(pattern, slide.Notes ?? string.Empty).Count > 0;
    }

    public (RichText Text, int Count) Replace(RichText text, string query, string replacement, bool matchCase) =>
        Replace(text, new TextPattern(query, matchCase), replacement);

    public (RichText Text, int Count) Replace(RichText text, TextPattern pattern, string replacement)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (pattern.IsEmpty) return (text, 0);

        var count = 0;
        var paragraphs = text.Paragraphs.Select(paragraph =>
        {
            var joined = Joined(paragraph);
            var hits = matcher.Find(pattern, joined, replacement);
            if (hits.Count == 0) return paragraph;
            count += hits.Count;

            if (paragraph.Runs.Count > 0)
            {
                var runs = paragraph.Runs.ToList();
                for (var index = hits.Count - 1; index >= 0; index--)
                    runs = Splice(runs, hits[index].Start, hits[index].Length, hits[index].Replacement);
                return paragraph.WithRuns([.. runs.Where(run => run.Text.Length > 0)]);
            }

            var rebuilt = joined;
            for (var index = hits.Count - 1; index >= 0; index--)
                rebuilt = rebuilt[..hits[index].Start] + hits[index].Replacement + rebuilt[(hits[index].Start + hits[index].Length)..];
            return paragraph with { Text = rebuilt };
        }).ToList();

        return count == 0 ? (text, 0) : (text with { Paragraphs = paragraphs }, count);
    }

    private string Joined(TextParagraph paragraph) =>
        paragraph.Runs.Count > 0 ? string.Concat(paragraph.Runs.Select(run => run.Text)) : paragraph.Text;

    private List<TextRun> Splice(List<TextRun> runs, int start, int length, string replacement)
    {
        var result = new List<TextRun>();
        var position = 0;
        var placed = false;

        foreach (var run in runs)
        {
            var runStart = position;
            var runEnd = position + run.Text.Length;
            position = runEnd;

            var cutFrom = Math.Clamp(start - runStart, 0, run.Text.Length);
            var cutTo = Math.Clamp(start + length - runStart, 0, run.Text.Length);

            var containsStart = !placed && start >= runStart && start < runEnd;
            if (cutTo <= cutFrom && !containsStart)
            {
                result.Add(run);
                continue;
            }

            placed |= containsStart;
            result.Add(run with { Text = run.Text[..cutFrom] + (containsStart ? replacement : string.Empty) + run.Text[cutTo..] });
        }

        return result;
    }
}

public sealed record DeckTextMatch(int SlideIndex, string? ElementId, int Paragraph, int Offset)
{
    public int Row { get; init; }

    public int Column { get; init; } = -1;

    public int Length { get; init; }

    public bool InNotes => ElementId is null;

    public bool InCell => Column >= 0;
}
