using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public enum TextIterationUnit
{
    None,
    Letter,
    Word,
}

public readonly record struct TextReveal(TextIterationUnit Unit, bool Backwards, bool Exiting, int ElapsedMs, int IntervalMs, double Progress)
{
    public bool IsWhole => Unit == TextIterationUnit.None;
}

public interface ITextIterationReveal
{
    TextIterationUnit UnitOf(AnimationEffect effect);

    IReadOnlyList<bool> Visible(string text, TextReveal reveal);

    RichText Reveal(RichText text, TextReveal reveal);
}

public sealed class TextIterationReveal : ITextIterationReveal
{
    public TextIterationUnit UnitOf(AnimationEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return effect.Native?.Behaviours?.IterateType switch
        {
            "by-letter" => TextIterationUnit.Letter,
            "by-word" => TextIterationUnit.Word,
            _ => TextIterationUnit.None,
        };
    }

    public IReadOnlyList<bool> Visible(string text, TextReveal reveal)
    {
        ArgumentNullException.ThrowIfNull(text);
        var units = Units(text, reveal.Unit);
        var mask = new bool[text.Length];
        if (reveal.IsWhole || units.Length == 0)
        {
            Array.Fill(mask, true);
            return mask;
        }

        var count = units.Max() + 1;
        var started = reveal.IntervalMs > 0 ? Math.Max(0, reveal.ElapsedMs) / reveal.IntervalMs + 1 : count;
        started = Math.Clamp(Math.Max(started, (int)Math.Ceiling(Math.Clamp(reveal.Progress, 0, 1) * count)), 0, count);
        for (var index = 0; index < text.Length; index++)
        {
            if (units[index] < 0)
            {
                mask[index] = reveal.Exiting;
                continue;
            }

            var order = reveal.Backwards ? count - 1 - units[index] : units[index];
            mask[index] = order < started != reveal.Exiting;
        }

        return mask;
    }

    public RichText Reveal(RichText text, TextReveal reveal)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (reveal.IsWhole) return text;
        var runs = text.Paragraphs.Select(paragraph => paragraph.EffectiveRuns()).ToList();
        var mask = Visible(string.Join("\n", runs.Select(paragraph => string.Concat(paragraph.Select(run => run.Text)))), reveal);
        var offset = 0;
        var paragraphs = new List<TextParagraph>(text.Paragraphs.Count);
        for (var index = 0; index < runs.Count; index++)
        {
            var pieces = new List<TextRun>();
            foreach (var run in runs[index])
            {
                if (run.Field != TextFieldKind.None || run.Text.Length == 0)
                {
                    pieces.Add(offset < mask.Count && run.Text.Length > 0 && !mask[offset] ? Hidden(run) : run);
                    offset += run.Text.Length;
                    continue;
                }

                var start = 0;
                for (var position = 1; position <= run.Text.Length; position++)
                {
                    if (position < run.Text.Length && mask[offset + position] == mask[offset + start]) continue;
                    var piece = run with { Text = run.Text[start..position] };
                    pieces.Add(mask[offset + start] ? piece : Hidden(piece));
                    start = position;
                }

                offset += run.Text.Length;
            }

            offset++;
            var shown = pieces.Any(piece => piece.Text.Length > 0 && piece.Color != 0u);
            var paragraph = text.Paragraphs[index] with { Runs = pieces };
            paragraphs.Add(shown || pieces.Count == 0 ? paragraph : paragraph with
            {
                Color = 0u,
                ColorRole = null,
                BulletSymbol = paragraph.BulletSymbol is { } symbol ? symbol with { Color = 0u } : null,
            });
        }

        return text with { Paragraphs = paragraphs };
    }

    private TextRun Hidden(TextRun run) => run with
    {
        Color = 0u,
        ColorRole = null,
        Highlight = null,
        Hyperlink = null,
        Shadow = false,
        Outline = false,
    };

    private int[] Units(string text, TextIterationUnit unit)
    {
        var units = new int[text.Length];
        var next = 0;
        var inWord = false;
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                units[index] = -1;
                inWord = false;
                continue;
            }

            if (unit == TextIterationUnit.Letter)
            {
                if (char.IsLowSurrogate(text[index]) && index > 0 && units[index - 1] >= 0) units[index] = units[index - 1];
                else units[index] = next++;
                continue;
            }

            if (!inWord) next++;
            inWord = true;
            units[index] = next - 1;
        }

        return units;
    }
}
