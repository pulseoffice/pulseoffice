using PulseOffice.Text.Spelling;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public sealed record DeckMistake(Slide Slide, string ElementId, int Paragraph, SpellingMistake Mistake);

public interface IDeckSpelling
{
    IReadOnlyList<WordSpan> Misspelled(string text, LanguageTag language);

    IEnumerable<DeckMistake> Walk(Presentation deck, int fromSlide, LanguageTag language);

    RichText Replace(RichText text, int paragraph, WordSpan word, string replacement);

    LanguageTag LanguageOf(Presentation deck);
}

public sealed class DeckSpelling(ISpellChecker checker, ITextLanguageResolver languages) : IDeckSpelling
{
    public LanguageTag LanguageOf(Presentation deck)
    {
        ArgumentNullException.ThrowIfNull(deck);
        return languages.Resolve(deck.TextLanguage);
    }

    public IReadOnlyList<WordSpan> Misspelled(string text, LanguageTag language) =>
        [.. checker.Check(text, language, suggestions: 0).Select(mistake => mistake.Word)];

    public IEnumerable<DeckMistake> Walk(Presentation deck, int fromSlide, LanguageTag language)
    {
        ArgumentNullException.ThrowIfNull(deck);
        var count = deck.Slides.Count;
        if (count == 0) yield break;

        var start = Math.Clamp(fromSlide, 0, count - 1);
        for (var step = 0; step < count; step++)
        {
            var slide = deck.Slides[(start + step) % count];
            foreach (var element in slide.InZOrder().OfType<TextElement>())
            {
                for (var index = 0; index < element.Text.Paragraphs.Count; index++)
                {
                    var paragraph = element.Text.Paragraphs[index];
                    if (paragraph.EffectiveRuns().Any(run => run.Field != TextFieldKind.None)) continue;

                    foreach (var mistake in checker.Check(paragraph.Text, language))
                        yield return new DeckMistake(slide, element.Id, index, mistake);
                }
            }
        }
    }

    public RichText Replace(RichText text, int paragraph, WordSpan word, string replacement)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (paragraph < 0 || paragraph >= text.Paragraphs.Count) return text;

        var source = text.Paragraphs[paragraph];
        var runs = new List<TextRun>();
        var position = 0;
        var inserted = false;
        var end = word.Start + word.Length;

        foreach (var run in source.EffectiveRuns())
        {
            var from = position;
            var to = position + run.Text.Length;
            position = to;

            if (to <= word.Start || from >= end)
            {
                runs.Add(run);
                continue;
            }

            var before = run.Text[..Math.Max(0, word.Start - from)];
            var after = run.Text[Math.Min(run.Text.Length, end - from)..];
            var middle = inserted ? string.Empty : replacement;
            inserted = true;

            var rewritten = before + middle + after;
            if (rewritten.Length > 0) runs.Add(run with { Text = rewritten });
        }

        var updated = source.WithRuns(runs);
        return text with { Paragraphs = [.. text.Paragraphs.Select((p, i) => i == paragraph ? updated : p)] };
    }
}
