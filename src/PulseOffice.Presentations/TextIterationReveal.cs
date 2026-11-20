using System.Globalization;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Text.Rich;

namespace PulseOffice.Presentations;

public enum TextIterationUnit
{
    None,
    Letter,
    Word,
}

public readonly record struct TextReveal(
    TextIterationUnit Unit, bool Backwards, bool Exiting, int ElapsedMs, int IntervalMs, int UnitMs = 0, Func<double, double>? Opacity = null)
{
    public bool IsWhole => Unit == TextIterationUnit.None;
}

public interface ITextIterationReveal
{
    TextIterationUnit UnitOf(AnimationEffect effect);

    int UnitCount(SlideElement element, TextIterationUnit unit);

    IReadOnlyList<double> Opacities(string text, TextReveal reveal);

    IReadOnlyList<bool> Visible(string text, TextReveal reveal);

    RichText Reveal(RichText text, TextReveal reveal, ITextTheme theme);

    ShapeObject Reveal(ShapeObject shape, TextReveal reveal);
}

public sealed class TextIterationReveal : ITextIterationReveal
{
    private const double Unseen = 0.5 / 255;

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

    public int UnitCount(SlideElement element, TextIterationUnit unit)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (unit == TextIterationUnit.None) return 1;
        var text = element switch
        {
            TextElement own => string.Join("\n", own.Text.Paragraphs.Select(paragraph => string.Concat(paragraph.EffectiveRuns().Select(run => run.Text)))),
            ObjectElement { Content: ShapeObject shape } => ShapeContent(shape),
            _ => string.Empty,
        };
        var units = Units(text, unit);
        return units.Length == 0 ? 1 : Math.Max(1, units.Max() + 1);
    }

    public IReadOnlyList<double> Opacities(string text, TextReveal reveal)
    {
        ArgumentNullException.ThrowIfNull(text);
        var units = Units(text, reveal.Unit);
        var opacities = new double[text.Length];
        if (reveal.IsWhole || units.Length == 0)
        {
            Array.Fill(opacities, 1.0);
            return opacities;
        }

        var count = units.Max() + 1;
        var interval = Math.Max(0, reveal.IntervalMs);
        var elapsed = Math.Max(0, reveal.ElapsedMs);
        for (var index = 0; index < text.Length; index++)
        {
            if (units[index] < 0)
            {
                opacities[index] = reveal.Exiting ? 1 : 0;
                continue;
            }

            var order = reveal.Backwards ? count - 1 - units[index] : units[index];
            opacities[index] = UnitOpacity(reveal, elapsed - (long)order * interval);
        }

        return opacities;
    }

    public IReadOnlyList<bool> Visible(string text, TextReveal reveal) =>
        [.. Opacities(text, reveal).Select(opacity => opacity > Unseen)];

    public RichText Reveal(RichText text, TextReveal reveal, ITextTheme theme)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(theme);
        if (reveal.IsWhole) return text;
        var runs = text.Paragraphs.Select(paragraph => paragraph.EffectiveRuns()).ToList();
        var opacities = Opacities(string.Join("\n", runs.Select(paragraph => string.Concat(paragraph.Select(run => run.Text)))), reveal);
        var offset = 0;
        var paragraphs = new List<TextParagraph>(text.Paragraphs.Count);
        for (var index = 0; index < runs.Count; index++)
        {
            var paragraph = text.Paragraphs[index];
            var pieces = new List<TextRun>();
            var seen = false;
            foreach (var run in runs[index])
            {
                if (run.Field != TextFieldKind.None || run.Text.Length == 0)
                {
                    var opacity = offset < opacities.Count && run.Text.Length > 0 ? opacities[offset] : 1;
                    seen |= run.Text.Length > 0 && opacity > Unseen;
                    pieces.Add(Faded(run, paragraph, opacity, theme));
                    offset += run.Text.Length;
                    continue;
                }

                var start = 0;
                for (var position = 1; position <= run.Text.Length; position++)
                {
                    if (position < run.Text.Length && Alpha(opacities[offset + position]) == Alpha(opacities[offset + start])) continue;
                    var opacity = opacities[offset + start];
                    seen |= opacity > Unseen && !string.IsNullOrWhiteSpace(run.Text[start..position]);
                    pieces.Add(Faded(run with { Text = run.Text[start..position] }, paragraph, opacity, theme));
                    start = position;
                }

                offset += run.Text.Length;
            }

            offset++;
            var revealed = paragraph with { Runs = pieces };
            paragraphs.Add(seen || pieces.Count == 0 ? revealed : revealed with
            {
                Color = 0u,
                ColorRole = null,
                BulletSymbol = (paragraph.BulletSymbol ?? (paragraph.Bullet ? new BulletSymbol("•") : null)) is { } symbol ? symbol with { Color = 0u } : null,
            });
        }

        return text with { Paragraphs = paragraphs };
    }

    public ShapeObject Reveal(ShapeObject shape, TextReveal reveal)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var content = ShapeContent(shape);
        if (reveal.IsWhole || content.Length == 0) return shape;
        var opacities = Opacities(content, reveal);
        var source = shape.Runs is { Count: > 0 } own ? own : [new ShapeTextRun(content)];
        var runs = new List<ShapeTextRun>();
        var offset = 0;
        foreach (var run in source)
        {
            var start = 0;
            for (var position = 1; position <= run.Text.Length; position++)
            {
                if (position < run.Text.Length && Alpha(opacities[offset + position]) == Alpha(opacities[offset + start])) continue;
                runs.Add(Faded(run with { Text = run.Text[start..position] }, shape.Style.Text.ColorHex, opacities[offset + start]));
                start = position;
            }

            offset += run.Text.Length;
        }

        return shape with { Runs = runs, Lists = HiddenBullets(shape, content, opacities) };
    }

    private IReadOnlyList<ShapeListItem?>? HiddenBullets(ShapeObject shape, string content, IReadOnlyList<double> opacities)
    {
        if (shape.Lists is not { Count: > 0 } lists) return shape.Lists;
        var items = lists.ToList();
        var offset = 0;
        var paragraphs = content.Split('\n');
        for (var index = 0; index < paragraphs.Length && index < items.Count; index++)
        {
            var paragraph = paragraphs[index];
            var seen = Enumerable.Range(offset, paragraph.Length).Any(position => opacities[position] > Unseen && !char.IsWhiteSpace(content[position]));
            if (!seen && items[index] is { Numbered: false } item)
                items[index] = item with { Symbol = (item.Symbol ?? new BulletSymbol("•")) with { Color = 0u } };
            offset += paragraph.Length + 1;
        }

        return items;
    }

    private string ShapeContent(ShapeObject shape) =>
        shape.Runs is { Count: > 0 } runs ? string.Concat(runs.Select(run => run.Text)) : shape.Text ?? string.Empty;

    private double UnitOpacity(TextReveal reveal, long sinceStart)
    {
        if (sinceStart < 0) return reveal.Exiting ? 1 : 0;
        var progress = reveal.UnitMs <= 0 ? 1 : Math.Clamp((double)sinceStart / reveal.UnitMs, 0, 1);
        if (reveal.Opacity is { } curve) return Math.Clamp(curve(progress), 0, 1);
        return reveal.Exiting ? 0 : 1;
    }

    private byte Alpha(double opacity) => (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);

    private TextRun Faded(TextRun run, TextParagraph paragraph, double opacity, ITextTheme theme)
    {
        var alpha = Alpha(opacity);
        if (alpha == 255) return run;
        if (alpha == 0) return Hidden(run);
        var ink = run.Hyperlink is not null
            ? theme.Resolve(ColorRole.Hyperlink)
            : run.Color ?? (run.ColorRole is { } role ? theme.Resolve(role) : paragraph.Color ?? theme.Resolve(paragraph.ColorRole ?? ColorRole.Text));
        return run with { Color = WithAlpha(ink, alpha), ColorRole = null, Hyperlink = null };
    }

    private ShapeTextRun Faded(ShapeTextRun run, string fallbackHex, double opacity)
    {
        var alpha = Alpha(opacity);
        if (alpha == 255) return run;
        var ink = Argb(run.ColorHex) ?? Argb(fallbackHex) ?? 0xFF000000u;
        return run with { ColorHex = "#" + WithAlpha(ink, alpha).ToString("X8", CultureInfo.InvariantCulture), Link = null };
    }

    private uint WithAlpha(uint argb, byte alpha) => ((uint)Math.Round((argb >> 24) * alpha / 255.0) << 24) | (argb & 0x00FFFFFFu);

    private uint? Argb(string? hex)
    {
        var digits = (hex ?? string.Empty).TrimStart('#');
        if (!uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)) return null;
        return digits.Length switch
        {
            6 => 0xFF000000u | value,
            8 => value,
            _ => null,
        };
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
