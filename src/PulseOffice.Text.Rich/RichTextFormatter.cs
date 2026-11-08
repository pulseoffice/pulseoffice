namespace PulseOffice.Text.Rich;

public enum CharAttribute
{
    Bold,
    Italic,
    Underline,
    Strikethrough,
    Superscript,
    Subscript,
    DoubleUnderline,
    Overline,
    Shadow,
    Outline,
    SmallCaps,
}

public sealed record CharacterFormat(
    string? Font, double? Size, bool? Bold, bool? Italic, bool? Underline, bool? Strikethrough,
    bool? DoubleUnderline, bool? Overline, bool? Shadow, bool? Outline, bool? SmallCaps,
    uint? Color = null, uint? Highlight = null);

public sealed record ParagraphFormat(
    TextAlign? Align, double? LineSpacing, bool? Bullet, int? Level, double? MarginLeft = null, double? Indent = null)
{
    public Bidi.TextDirection? Direction { get; init; }
}

public interface IRichTextFormatter
{
    RichText ToggleChar(RichText text, (int Start, int End)? range, CharAttribute attribute);

    RichText SetChar(RichText text, (int Start, int End)? range, CharAttribute attribute, bool on);

    RichText SetFont(RichText text, (int Start, int End)? range, string family);

    RichText SetFontSize(RichText text, (int Start, int End)? range, double size);

    RichText SetBullet(RichText text, (int Start, int End)? range, bool bullet);

    RichText SetBulletLevel(RichText text, (int Start, int End)? range, int level);

    CharacterFormat ReadCharacter(RichText text, (int Start, int End)? range);

    ParagraphFormat ReadParagraph(RichText text, (int Start, int End)? range);

    RichText SetColor(RichText text, (int Start, int End)? range, uint? color, ColorRole? role);

    RichText SetHighlight(RichText text, (int Start, int End)? range, uint? highlight);

    RichText Scale(RichText text, (int Start, int End)? range, double factor);

    RichText SetAlign(RichText text, (int Start, int End)? range, TextAlign align);

    RichText SetDirection(RichText text, (int Start, int End)? range, Bidi.TextDirection direction);

    RichText ToggleBullets(RichText text, (int Start, int End)? range);

    RichText Indent(RichText text, (int Start, int End)? range, int delta);

    RichText Transform(RichText text, (int Start, int End)? range, Func<string, string> convert);

    RichText ReplaceWord(RichText text, (int Start, int End) range, string replacement);

    RichText SetLineSpacing(RichText text, (int Start, int End)? range, double spacing);

    RichText ToggleNumbering(RichText text, (int Start, int End)? range);

    RichText AdjustSpacing(RichText text, (int Start, int End)? range, double deltaPoints);

    RichText ClearDirectFormatting(RichText text, (int Start, int End)? range);

    RichText CycleCase(RichText text, (int Start, int End)? range);

    RichText MoveParagraphs(RichText text, (int Start, int End)? range, int delta);

    RichText SetList(RichText text, (int Start, int End)? range, bool bullet, bool numbered);

    RichText SetIndents(RichText text, (int Start, int End)? range, double marginLeft, double indent);

    RichText SetAnchor(RichText text, TextAnchor anchor);

    TextParagraph? ParagraphAt(RichText text, (int Start, int End)? range);

    RichText SetRightIndent(RichText text, (int Start, int End)? range, double marginRight);

    RichText SetTabStops(RichText text, (int Start, int End)? range, IReadOnlyList<TabStop> stops);

    RichText SetFrame(RichText text, TextFrame frame);

    RichText MapParagraphs(RichText text, (int Start, int End)? range, Func<TextParagraph, TextParagraph> change);

    RichText MapRuns(RichText text, (int Start, int End)? range, Func<TextRun, TextRun> change);

    IReadOnlyList<TextParagraph> ParagraphsIn(RichText text, (int Start, int End)? range);

    IReadOnlyList<TextRun> RunsIn(RichText text, (int Start, int End)? range);
}

public sealed class RichTextFormatter : IRichTextFormatter
{
    private readonly PulseOffice.Text.ITextCases _cases;
    private readonly TextRangeWalker _ranges = new(new RunMerger());
    private readonly RunAttributes _attributes;
    private readonly CommonValue _common = new();

    public RichTextFormatter() : this(new PulseOffice.Text.TextCases())
    {
    }

    public RichTextFormatter(PulseOffice.Text.ITextCases cases)
    {
        _cases = cases;
        _attributes = new RunAttributes(_ranges);
    }

    public RichText ToggleChar(RichText text, (int Start, int End)? range, CharAttribute attribute)
    {
        var turnOn = !_attributes.AllHave(text, range, attribute);
        return _ranges.MapRuns(text, range, run => _attributes.With(run, attribute, turnOn));
    }

    public RichText SetChar(RichText text, (int Start, int End)? range, CharAttribute attribute, bool on) =>
        _ranges.MapRuns(text, range, run => _attributes.With(run, attribute, on));

    public RichText SetFont(RichText text, (int Start, int End)? range, string family)
    {
        ArgumentNullException.ThrowIfNull(family);
        return _ranges.MapRuns(text, range, run => run with { Font = string.IsNullOrWhiteSpace(family) ? null : family });
    }

    public RichText SetFontSize(RichText text, (int Start, int End)? range, double size) =>
        double.IsFinite(size) ? _ranges.MapRuns(text, range, run => run with { FontSize = Math.Clamp(size, 6, 300) }) : text;

    public RichText SetBullet(RichText text, (int Start, int End)? range, bool bullet) =>
        _ranges.MapParagraphs(text, range, paragraph => paragraph with { Bullet = bullet });

    public RichText SetBulletLevel(RichText text, (int Start, int End)? range, int level) =>
        _ranges.MapParagraphs(text, range, paragraph => paragraph with { BulletLevel = Math.Clamp(level, 0, 8) });

    public CharacterFormat ReadCharacter(RichText text, (int Start, int End)? range)
    {
        var runs = range is { } caret && caret.Start == caret.End
            ? _ranges.RunAt(text, caret.Start).ToList()
            : _ranges.Runs(text, range).Where(run => run.Text.Length > 0).ToList();
        if (runs.Count == 0) runs = _ranges.Runs(text, null).ToList();

        return new CharacterFormat(
            _common.Of(runs.Select(run => run.Font)),
            _common.Of(runs.Select(run => run.FontSize)),
            _common.Of(runs.Select(run => run.Bold)),
            _common.Of(runs.Select(run => run.Italic)),
            _common.Of(runs.Select(run => run.Underline)),
            _common.Of(runs.Select(run => run.Strikethrough)),
            _common.Of(runs.Select(run => run.DoubleUnderline)),
            _common.Of(runs.Select(run => run.Overline)),
            _common.Of(runs.Select(run => run.Shadow)),
            _common.Of(runs.Select(run => run.Outline)),
            _common.Of(runs.Select(run => run.SmallCaps)),
            _common.Of(runs.Select(run => run.Color)),
            _common.Of(runs.Select(run => run.Highlight)));
    }

    public ParagraphFormat ReadParagraph(RichText text, (int Start, int End)? range)
    {
        var paragraphs = _ranges.Paragraphs(text, range).ToList();

        return new ParagraphFormat(
            _common.Of(paragraphs.Select(p => p.Align)),
            _common.Of(paragraphs.Select(p => p.LineSpacing)),
            _common.Of(paragraphs.Select(p => p.Bullet)),
            _common.Of(paragraphs.Select(p => p.BulletLevel)),
            _common.Of(paragraphs.Select(p => p.MarginLeft ?? p.BulletLevel * 18.0)),
            _common.Of(paragraphs.Select(p => p.Indent ?? 0.0)))
        {
            Direction = _common.Of(paragraphs.Select(p => p.Direction ?? Bidi.TextDirection.LeftToRight)),
        };
    }

    public RichText SetIndents(RichText text, (int Start, int End)? range, double marginLeft, double indent) =>
        _ranges.MapParagraphs(text, range, paragraph => paragraph with { MarginLeft = Math.Max(0, marginLeft), Indent = indent });

    public TextParagraph? ParagraphAt(RichText text, (int Start, int End)? range) =>
        _ranges.Paragraphs(text, range).FirstOrDefault();

    public RichText SetRightIndent(RichText text, (int Start, int End)? range, double marginRight) =>
        _ranges.MapParagraphs(text, range, paragraph => paragraph with { MarginRight = Math.Max(0, marginRight) });

    public RichText SetTabStops(RichText text, (int Start, int End)? range, IReadOnlyList<TabStop> stops) =>
        _ranges.MapParagraphs(text, range, paragraph => paragraph with { TabStops = [.. stops.OrderBy(stop => stop.Position)] });

    public RichText SetAnchor(RichText text, TextAnchor anchor) => text with { Frame = text.Frame with { Anchor = anchor } };

    public RichText SetFrame(RichText text, TextFrame frame) => text with { Frame = frame };

    public RichText MapParagraphs(RichText text, (int Start, int End)? range, Func<TextParagraph, TextParagraph> change) =>
        _ranges.MapParagraphs(text, range, change);

    public RichText MapRuns(RichText text, (int Start, int End)? range, Func<TextRun, TextRun> change) =>
        _ranges.MapRuns(text, range, change);

    public IReadOnlyList<TextParagraph> ParagraphsIn(RichText text, (int Start, int End)? range) =>
        [.. _ranges.Paragraphs(text, range)];

    public IReadOnlyList<TextRun> RunsIn(RichText text, (int Start, int End)? range)
    {
        var runs = range is { } caret && caret.Start == caret.End
            ? _ranges.RunAt(text, caret.Start).ToList()
            : _ranges.Runs(text, range).Where(run => run.Text.Length > 0).ToList();
        return runs.Count == 0 ? [.. _ranges.Runs(text, null)] : runs;
    }

    public RichText SetColor(RichText text, (int Start, int End)? range, uint? color, ColorRole? role) =>
        _ranges.MapRuns(text, range, run => run with { Color = color, ColorRole = color is null ? role : null });

    public RichText SetHighlight(RichText text, (int Start, int End)? range, uint? highlight) =>
        _ranges.MapRuns(text, range, run => run with { Highlight = highlight });

    public RichText Scale(RichText text, (int Start, int End)? range, double factor) =>
        _ranges.MapRuns(text, range, run => run with
        {
            FontSize = Math.Clamp((run.FontSize ?? 18) * factor, 6, 300),
        });

    public RichText SetAlign(RichText text, (int Start, int End)? range, TextAlign align) =>
        _ranges.MapParagraphs(text, range, paragraph => paragraph with { Align = align });

    public RichText SetDirection(RichText text, (int Start, int End)? range, Bidi.TextDirection direction) =>
        _ranges.MapParagraphs(text, range, paragraph => paragraph with { Direction = direction });

    public RichText ToggleBullets(RichText text, (int Start, int End)? range)
    {
        var allBulleted = _ranges.Paragraphs(text, range).All(p => p.Bullet && !p.Numbered);
        return _ranges.MapParagraphs(text, range, paragraph => paragraph with { Bullet = !allBulleted, Numbered = false });
    }

    public RichText SetList(RichText text, (int Start, int End)? range, bool bullet, bool numbered) =>
        _ranges.MapParagraphs(text, range, paragraph => paragraph with { Bullet = bullet && !numbered, Numbered = numbered });

    public RichText ToggleNumbering(RichText text, (int Start, int End)? range)
    {
        var allNumbered = _ranges.Paragraphs(text, range).All(p => p.Numbered);
        return _ranges.MapParagraphs(text, range, paragraph => paragraph with { Numbered = !allNumbered, Bullet = false });
    }

    public RichText AdjustSpacing(RichText text, (int Start, int End)? range, double deltaPoints) =>
        _ranges.MapParagraphs(text, range, paragraph => paragraph with
        {
            SpaceBefore = Math.Clamp(paragraph.SpaceBefore + deltaPoints, 0, 200),
            SpaceAfter = Math.Clamp(paragraph.SpaceAfter + deltaPoints, 0, 200),
        });

    public RichText ClearDirectFormatting(RichText text, (int Start, int End)? range) =>
        _ranges.MapRuns(text, range, (paragraph, run) => paragraph.SingleRun() with { Text = run.Text, Field = run.Field });

    public RichText CycleCase(RichText text, (int Start, int End)? range)
    {
        var sample = string.Concat(_ranges.Runs(text, range).Select(run => run.Text));
        return Transform(text, range, _cases.NextInCycle(sample).Apply);
    }

    public RichText MoveParagraphs(RichText text, (int Start, int End)? range, int delta)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (range is null || delta == 0) return text;

        var touched = _ranges.Indices(text, range);
        if (touched.Count == 0) return text;

        var first = touched[0];
        var last = touched[^1];
        var paragraphs = text.Paragraphs.ToList();
        if (delta < 0 && first == 0) return text;
        if (delta > 0 && last == paragraphs.Count - 1) return text;

        var block = paragraphs.GetRange(first, last - first + 1);
        paragraphs.RemoveRange(first, block.Count);
        paragraphs.InsertRange(first + Math.Sign(delta), block);
        return text with { Paragraphs = paragraphs };
    }

    public RichText Indent(RichText text, (int Start, int End)? range, int delta) =>
        _ranges.MapParagraphs(text, range, paragraph => paragraph with
        {
            BulletLevel = Math.Clamp(paragraph.BulletLevel + delta, 0, 8),
        });

    public RichText Transform(RichText text, (int Start, int End)? range, Func<string, string> convert)
    {
        ArgumentNullException.ThrowIfNull(convert);

        var joined = new System.Text.StringBuilder();
        TextParagraph? owner = null;
        _ranges.MapRuns(text, range, (paragraph, run) =>
        {
            if (owner is not null && !ReferenceEquals(owner, paragraph)) joined.Append('\n');
            owner = paragraph;
            joined.Append(run.Text);
            return run;
        });

        var original = joined.ToString();
        var converted = convert(original);
        if (converted.Length != original.Length) return _ranges.MapRuns(text, range, run => run with { Text = convert(run.Text) });

        var cursor = 0;
        owner = null;
        return _ranges.MapRuns(text, range, (paragraph, run) =>
        {
            if (owner is not null && !ReferenceEquals(owner, paragraph)) cursor++;
            owner = paragraph;
            var piece = converted.Substring(cursor, run.Text.Length);
            cursor += run.Text.Length;
            return run with { Text = piece };
        });
    }

    public RichText ReplaceWord(RichText text, (int Start, int End) range, string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var plain = text.PlainText;
        var start = Math.Clamp(range.Start, 0, plain.Length);
        var end = Math.Clamp(range.End, start, plain.Length);
        while (start < end && char.IsWhiteSpace(plain[start])) start++;
        while (end > start && char.IsWhiteSpace(plain[end - 1])) end--;
        if (end <= start) return text;

        var placed = false;
        return _ranges.MapRuns(text, (start, end), run =>
        {
            if (placed) return run with { Text = string.Empty };
            placed = true;
            return run with { Text = replacement };
        });
    }

    public RichText SetLineSpacing(RichText text, (int Start, int End)? range, double spacing) =>
        double.IsFinite(spacing) ? _ranges.MapParagraphs(text, range, paragraph => paragraph with { LineSpacing = Math.Clamp(spacing, 0.1, 10) }) : text;
}

internal sealed class CommonValue
{
    public T? Of<T>(IEnumerable<T> values) where T : struct => Common(values, out var value) ? value : null;

    public T? Of<T>(IEnumerable<T?> values) where T : struct => Common(values, out var value) ? value : null;

    public string? Of(IEnumerable<string?> values) => Common(values, out var value) ? value : null;

    private bool Common<T>(IEnumerable<T> values, out T value)
    {
        using var e = values.GetEnumerator();
        value = default!;
        if (!e.MoveNext()) return false;

        value = e.Current;
        while (e.MoveNext())
            if (!EqualityComparer<T>.Default.Equals(e.Current, value)) return false;

        return true;
    }
}

internal abstract class RunAttribute(CharAttribute attribute)
{
    public CharAttribute Attribute => attribute;

    public abstract bool Reads(TextRun run);

    public abstract TextRun With(TextRun run, bool on);
}

internal sealed class FlagAttribute(CharAttribute attribute, Func<TextRun, bool> read, Func<TextRun, bool, TextRun> write)
    : RunAttribute(attribute)
{
    public override bool Reads(TextRun run) => read(run);

    public override TextRun With(TextRun run, bool on) => write(run, on);
}

internal sealed class BaselineAttribute(CharAttribute attribute, TextBaseline baseline) : RunAttribute(attribute)
{
    public override bool Reads(TextRun run) => run.Baseline == baseline;

    public override TextRun With(TextRun run, bool on) => run with { Baseline = on ? baseline : TextBaseline.Normal };
}

internal sealed class RunAttributes(TextRangeWalker ranges)
{
    private readonly IReadOnlyDictionary<CharAttribute, RunAttribute> _attributes = new RunAttribute[]
    {
        new FlagAttribute(CharAttribute.Bold, run => run.Bold, (run, on) => run with { Bold = on }),
        new FlagAttribute(CharAttribute.Italic, run => run.Italic, (run, on) => run with { Italic = on }),
        new FlagAttribute(CharAttribute.Underline, run => run.Underline, (run, on) => run with { Underline = on }),
        new FlagAttribute(CharAttribute.Strikethrough, run => run.Strikethrough, (run, on) => run with { Strikethrough = on }),
        new BaselineAttribute(CharAttribute.Superscript, TextBaseline.Superscript),
        new BaselineAttribute(CharAttribute.Subscript, TextBaseline.Subscript),
        new FlagAttribute(CharAttribute.DoubleUnderline, run => run.DoubleUnderline,
            (run, on) => run with { DoubleUnderline = on, Underline = on && run.Underline }),
        new FlagAttribute(CharAttribute.Overline, run => run.Overline, (run, on) => run with { Overline = on }),
        new FlagAttribute(CharAttribute.Shadow, run => run.Shadow, (run, on) => run with { Shadow = on }),
        new FlagAttribute(CharAttribute.Outline, run => run.Outline, (run, on) => run with { Outline = on }),
        new FlagAttribute(CharAttribute.SmallCaps, run => run.SmallCaps, (run, on) => run with { SmallCaps = on }),
    }.ToDictionary(attribute => attribute.Attribute);

    public bool AllHave(RichText text, (int Start, int End)? range, CharAttribute attribute)
    {
        var runs = ranges.Runs(text, range).ToList();
        return runs.Count > 0 && _attributes.TryGetValue(attribute, out var reader) && runs.All(reader.Reads);
    }

    public TextRun With(TextRun run, CharAttribute attribute, bool on) =>
        _attributes.TryGetValue(attribute, out var writer) ? writer.With(run, on) : run;
}

internal sealed class RunMerger
{
    public TextParagraph Merge(TextParagraph paragraph)
    {
        if (paragraph.Runs.Count <= 1) return paragraph;

        return paragraph with { Runs = Merge(paragraph.Runs) };
    }

    public List<TextRun> Merge(IEnumerable<TextRun> runs)
    {
        var merged = new List<(TextRun Run, System.Text.StringBuilder Text)>();
        foreach (var current in runs)
        {
            if (merged.Count > 0 && SameStyle(merged[^1].Run, current)) merged[^1].Text.Append(current.Text);
            else merged.Add((current, new System.Text.StringBuilder(current.Text)));
        }

        return [.. merged.Select(entry => entry.Run with { Text = entry.Text.ToString() })];
    }

    private bool SameStyle(TextRun a, TextRun b) =>
        a.Field == TextFieldKind.None && b.Field == TextFieldKind.None
        && a with { Text = string.Empty } == b with { Text = string.Empty };
}

internal sealed class TextRangeWalker(RunMerger merger)
{
    public IEnumerable<TextRun> Runs(RichText text, (int Start, int End)? range)
    {
        var position = 0;
        foreach (var paragraph in text.Paragraphs)
        {
            foreach (var run in paragraph.EffectiveRuns())
            {
                var runStart = position;
                position += run.Text.Length;
                if (range is null || (position > range.Value.Start && runStart < range.Value.End))
                    yield return run;
            }

            position += 1;
        }
    }

    public IEnumerable<TextRun> RunAt(RichText text, int caret)
    {
        var position = 0;
        foreach (var paragraph in text.Paragraphs)
        {
            var runs = paragraph.EffectiveRuns();
            if (caret == position && runs.Count > 0)
                return [runs[0]];

            foreach (var run in runs)
            {
                var runStart = position;
                position += run.Text.Length;
                if (caret > runStart && caret <= position) return [run];
            }

            position += 1;
        }

        return [];
    }

    public IReadOnlyList<int> Indices(RichText text, (int Start, int End)? range) =>
        [.. Spans(text, Length).Select((span, index) => (span, index))
            .Where(item => Touches(item.span.Start, item.span.Paragraph, range)).Select(item => item.index)];

    public IEnumerable<TextParagraph> Paragraphs(RichText text, (int Start, int End)? range) =>
        Spans(text, Length).Where(span => Touches(span.Start, span.Paragraph, range)).Select(span => span.Paragraph);

    public RichText MapParagraphs(RichText text, (int Start, int End)? range, Func<TextParagraph, TextParagraph> transform) =>
        text with
        {
            Paragraphs = [.. Spans(text, Length).Select(span =>
                Touches(span.Start, span.Paragraph, range) ? transform(span.Paragraph) : span.Paragraph)],
        };

    public RichText MapRuns(RichText text, (int Start, int End)? range, Func<TextRun, TextRun> transform) =>
        MapRuns(text, range, (_, run) => transform(run));

    public RichText MapRuns(RichText text, (int Start, int End)? range, Func<TextParagraph, TextRun, TextRun> transform) =>
        text with
        {
            Paragraphs = [.. Spans(text, Length).Select(span =>
                merger.Merge(span.Paragraph.WithRuns([.. Splice(span.Paragraph, span.Start, range, transform)])))],
        };


    private int Length(TextParagraph paragraph) => paragraph.EffectiveRuns().Sum(run => run.Text.Length);

    private IEnumerable<TextRun> Splice(
        TextParagraph paragraph, int position, (int Start, int End)? range, Func<TextParagraph, TextRun, TextRun> transform)
    {
        foreach (var run in paragraph.EffectiveRuns())
        {
            var runStart = position;
            position += run.Text.Length;

            if (run.Text.Length == 0) { yield return range is null ? transform(paragraph, run) : run; continue; }
            if (range is null) { yield return transform(paragraph, run); continue; }

            var from = Math.Max(runStart, range.Value.Start);
            var to = Math.Min(position, range.Value.End);
            if (to <= from) { yield return run; continue; }

            var before = from - runStart;
            var inside = to - from;

            if (before > 0) yield return run with { Text = run.Text[..before] };
            yield return transform(paragraph, run with { Text = run.Text.Substring(before, inside) });
            if (before + inside < run.Text.Length) yield return run with { Text = run.Text[(before + inside)..] };
        }
    }

    private IEnumerable<(int Start, TextParagraph Paragraph)> Spans(RichText text, Func<TextParagraph, int> length)
    {
        var position = 0;
        foreach (var paragraph in text.Paragraphs)
        {
            yield return (position, paragraph);
            position += length(paragraph) + 1;
        }
    }

    private bool Touches(int start, TextParagraph paragraph, (int Start, int End)? range) =>
        range is null || (start + Length(paragraph) >= range.Value.Start
            && (start < range.Value.End || (start == range.Value.End && range.Value.Start == range.Value.End)));
}
