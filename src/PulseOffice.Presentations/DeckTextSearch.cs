using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public sealed class DeckTextSearch
{
    public int Count(RichText text, string query, bool matchCase)
    {
        if (string.IsNullOrEmpty(query)) return 0;
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        var total = 0;
        foreach (var paragraph in text.Paragraphs)
            total += CountIn(paragraph.Runs.Count > 0 ? string.Concat(paragraph.Runs.Select(run => run.Text)) : paragraph.Text, query, comparison);
        return total;
    }

    public IReadOnlyList<DeckTextMatch> Matches(IReadOnlyList<Slide> slides, string query, bool matchCase)
    {
        var found = new List<DeckTextMatch>();
        if (string.IsNullOrEmpty(query)) return found;
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        for (var slideIndex = 0; slideIndex < slides.Count; slideIndex++)
        {
            var slide = slides[slideIndex];
            foreach (var element in slide.Elements.OfType<TextElement>().OrderBy(e => e.ZOrder))
                for (var paragraph = 0; paragraph < element.Text.Paragraphs.Count; paragraph++)
                {
                    var p = element.Text.Paragraphs[paragraph];
                    var joined = p.Runs.Count > 0 ? string.Concat(p.Runs.Select(run => run.Text)) : p.Text;
                    foreach (var offset in Positions(joined, query, comparison))
                        found.Add(new DeckTextMatch(slideIndex, element.Id, paragraph, offset));
                }

            foreach (var offset in Positions(slide.Notes ?? string.Empty, query, comparison))
                found.Add(new DeckTextMatch(slideIndex, null, 0, offset));
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

    public bool SlideMatches(Slide slide, string query, bool matchCase) =>
        slide.Elements.OfType<TextElement>().Any(t => Count(t.Text, query, matchCase) > 0)
        || (!string.IsNullOrEmpty(query)
            && CountIn(slide.Notes ?? string.Empty, query, matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) > 0);

    public (RichText Text, int Count) Replace(RichText text, string query, string replacement, bool matchCase)
    {
        if (string.IsNullOrEmpty(query)) return (text, 0);
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        var count = 0;
        var paragraphs = text.Paragraphs.Select(paragraph =>
        {
            if (paragraph.Runs.Count > 0)
            {
                var joined = string.Concat(paragraph.Runs.Select(run => run.Text));
                var matches = Positions(joined, query, comparison);
                if (matches.Count == 0) return paragraph;
                count += matches.Count;

                var runs = paragraph.Runs.ToList();
                for (var index = matches.Count - 1; index >= 0; index--)
                    runs = Splice(runs, matches[index], query.Length, replacement);
                return paragraph.WithRuns([.. runs.Where(run => run.Text.Length > 0)]);
            }

            var occurrences = CountIn(paragraph.Text, query, comparison);
            if (occurrences == 0) return paragraph;
            count += occurrences;
            return paragraph with { Text = paragraph.Text.Replace(query, replacement, comparison) };
        }).ToList();

        return count == 0 ? (text, 0) : (text with { Paragraphs = paragraphs }, count);
    }

    private List<int> Positions(string source, string query, StringComparison comparison)
    {
        var found = new List<int>();
        var index = 0;
        while ((index = source.IndexOf(query, index, comparison)) >= 0)
        {
            found.Add(index);
            index += query.Length;
        }
        return found;
    }

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

    private int CountIn(string source, string query, StringComparison comparison)
    {
        if (string.IsNullOrEmpty(source)) return 0;
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(query, index, comparison)) >= 0)
        {
            count++;
            index += query.Length;
        }
        return count;
    }
}

public sealed record DeckTextMatch(int SlideIndex, string? ElementId, int Paragraph, int Offset)
{
    public bool InNotes => ElementId is null;
}
