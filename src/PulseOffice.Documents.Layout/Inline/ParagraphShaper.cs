using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Measurement;
using PulseOffice.Text.Rich;
using PulseOffice.Text.Spelling;

namespace PulseOffice.Documents.Layout.Inline;

public sealed record ShapingRequest(TextParagraph Paragraph, string DefaultFont, ListLabelPlacement? Label, ParagraphHyphenation? Hyphenation, string? Language);

public sealed record ListLabelPlacement(string Text, double Position)
{
    public bool FollowedByTab { get; init; } = true;

    public PulseOffice.Text.Rich.ListBulletPicture? Picture { get; init; }
}

public interface IParagraphShaper
{
    ShapedParagraph Shape(ShapingRequest request);
}

public interface IHyphenationPoints
{
    IReadOnlySet<int> Find(string text, string? language);
}

public sealed class NoHyphenation : IHyphenationPoints
{
    public IReadOnlySet<int> Find(string text, string? language) => new HashSet<int>();
}

public sealed class DictionaryHyphenation(IHyphenationProvider provider) : IHyphenationPoints
{
    public IReadOnlySet<int> Find(string text, string? language)
    {
        var points = new HashSet<int>();
        var hyphenator = provider.For(LanguageTag.Parse(language));
        if (hyphenator is null) return points;
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var letter = i < text.Length && char.IsLetter(text[i]);
            if (letter && start < 0) start = i;
            if (letter || start < 0) continue;
            foreach (var position in hyphenator.Positions(text[start..i])) points.Add(start + position);
            start = -1;
        }
        return points;
    }
}

public interface IHyphenationLimits
{
    IReadOnlySet<int> Apply(string text, IReadOnlySet<int> points, ParagraphHyphenation rules);
}

public sealed class HyphenationLimits : IHyphenationLimits
{
    public IReadOnlySet<int> Apply(string text, IReadOnlySet<int> points, ParagraphHyphenation rules)
    {
        var kept = new HashSet<int>();
        foreach (var point in points)
        {
            if (point <= 0 || point >= text.Length) continue;
            var start = point;
            while (start > 0 && IsWordCharacter(text[start - 1])) start--;
            var end = point;
            while (end < text.Length && IsWordCharacter(text[end])) end++;
            if (end - start < rules.MinimumWordLength) continue;
            if (point - start < rules.CharactersBefore || end - point < rules.CharactersAfter) continue;
            if (rules.SkipCapitalized && char.IsUpper(text[start])) continue;
            kept.Add(point);
        }
        return kept;
    }

    private bool IsWordCharacter(char character) => char.IsLetter(character) || char.GetUnicodeCategory(character) == System.Globalization.UnicodeCategory.NonSpacingMark;
}

public interface IDropCapShaper
{
    DropShape? Shape(DropCap cap, string text, double[] advances, ShapedRun first);
}

public sealed class DropCapShaper(ITextMeasurer measurer, Flow.LayoutOptions options) : IDropCapShaper
{
    public DropShape? Shape(DropCap cap, string text, double[] advances, ShapedRun first)
    {
        var length = cap.WholeWord ? WordLength(text) : Math.Min(Math.Max(0, cap.Characters), text.Length);
        if (length > 0 && length < text.Length && char.IsLowSurrogate(text[length])) length++;
        if (length <= 0) return null;
        var lineHeight = first.Metrics.Height;
        var lowering = (cap.Lines - 1) * lineHeight;
        var capHeight = first.Font.Size * options.CapHeight;
        var font = first.Font with { Size = (lowering + capHeight) / options.CapHeight };
        var measured = measurer.Advances(font, text[..length]);
        for (var i = 0; i < length; i++) advances[i] = 0;
        return new DropShape(length, font, measured, measured.Sum() + Math.Max(0, cap.Distance), cap.Lines, lowering);
    }

    private int WordLength(string text)
    {
        var length = 0;
        while (length < text.Length && !char.IsWhiteSpace(text[length]) && text[length] is not ('\t' or '\u2028')) length++;
        return length;
    }
}

public sealed class ParagraphShaper(ITextMeasurer measurer, IRunFonts fonts, IHyphenationPoints hyphenation, IHyphenationLimits limits, IRunTypesetter typesetter, IDropCapShaper drops, IBulletPictureSizing pictures) : IParagraphShaper
{

    public ShapedParagraph Shape(ShapingRequest request)
    {
        var source = request.Paragraph.EffectiveRuns();
        var length = source.Sum(run => run.Text.Length);
        var advances = new double[length];
        var runOf = new int[length];
        var display = new System.Text.StringBuilder(length);
        var runs = new List<ShapedRun>();
        var offset = 0;
        foreach (var run in source)
        {
            var font = fonts.FontOf(run, request.DefaultFont);
            var typeset = typesetter.Typeset(run, font);
            display.Append(typeset.Text);
            Array.Copy(typeset.Advances, 0, advances, offset, typeset.Advances.Length);
            foreach (var span in typeset.Spans)
            {
                for (var i = span.Start; i < span.End; i++) runOf[offset + i] = runs.Count;
                runs.Add(new ShapedRun(run, span.Font, fonts.ShiftOf(run), measurer.Metrics(span.Font), offset + span.Start, offset + span.End, measurer.Advances(span.Font, "-")[0]));
            }
            offset += run.Text.Length;
        }
        var text = display.ToString();
        var first = source.Count > 0 ? source[0] : request.Paragraph.SingleRun();
        var fallbackFont = fonts.FontOf(first with { FontSize = first.FontSize ?? request.Paragraph.FontSize }, request.DefaultFont);
        if (runs.Count == 0)
        {
            runs.Add(new ShapedRun(first, fallbackFont, 0, measurer.Metrics(fallbackFont), 0, 0, measurer.Advances(fallbackFont, "-")[0]));
        }
        var label = request.Label is null ? null : Label(request.Label, runs[0].Font with { Size = runs[0].Style.FontSize ?? fallbackFont.Size });
        var points = request.Hyphenation is { Enabled: true } rules ? limits.Apply(text, hyphenation.Find(text, request.Language), rules) : new HashSet<int>();
        var drop = request.Paragraph.DropCap is { IsActive: true } cap ? drops.Shape(cap, text, advances, runs[0]) : null;
        return new ShapedParagraph(text, advances, runOf, runs, measurer.Metrics(fallbackFont), label, points) { Drop = drop, Direction = request.Paragraph.Direction };
    }

    private ShapedLabel Label(ListLabelPlacement placement, FontKey font)
    {
        if (placement.Picture is { } picture)
        {
            var sized = pictures.Size(picture, font.Size);
            var text = measurer.Metrics(font);
            return new ShapedLabel(placement.Text, font, new FontMetrics(Math.Max(sized.Height, text.Ascent), text.Descent, text.LineGap), [sized.Width], placement.Position)
            {
                FollowedByTab = placement.FollowedByTab,
                Picture = sized,
            };
        }
        return new(placement.Text, font, measurer.Metrics(font), measurer.Advances(font, placement.Text), placement.Position) { FollowedByTab = placement.FollowedByTab };
    }
}
