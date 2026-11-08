using Avalonia;
using Avalonia.Media;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Text.Rich;

public sealed class RunText(TextFieldResolver resolver, ITextCultures cultures)
{
    public RunText() : this(new TextFieldResolver(), new TextCultures())
    {
    }

    public string Shown(TextRun run, TextFields fields)
    {
        var text = run.Field == TextFieldKind.None ? run.Text : resolver.Resolve(run, fields);
        return run.SmallCaps ? text.ToUpper(Culture(run, fields)) : text;
    }

    public System.Globalization.CultureInfo Culture(TextRun? run, TextFields fields) => cultures.For(run?.Language, fields.Language);
}

public sealed class ParagraphTypesetter(ITextTheme theme, double scale)
{
    public const double PointToTwip = 20.0;

    public const double MinimumEm = 4;

    public const double MaximumEm = 30000;

    private const double DefaultPoints = 18;

    public Color Ink(TextParagraph paragraph) =>
        Color.FromUInt32(paragraph.Color ?? theme.Resolve(paragraph.ColorRole ?? ColorRole.Text));

    public string Font(TextParagraph paragraph) => paragraph.MajorFont ? theme.MajorFont : theme.MinorFont;

    public double Points(double size) => size * PointToTwip * scale;

    public double Em(double size)
    {
        var em = Points(double.IsNaN(size) ? DefaultPoints : size);
        return double.IsFinite(em) ? Math.Clamp(em, MinimumEm, MaximumEm) : MinimumEm;
    }

    public FormattedText Typeset(string content, TextParagraph paragraph, Color color, double maxWidth, System.Globalization.CultureInfo culture) =>
        new(content,
            culture,
            Flow(paragraph),
            new Typeface($"{Font(paragraph)}, Inter, Helvetica, Arial",
                paragraph.Italic ? FontStyle.Italic : FontStyle.Normal,
                paragraph.Bold ? FontWeight.SemiBold : FontWeight.Normal),
            Em(paragraph.FontSize),
            new SolidColorBrush(color))
        {
            MaxTextWidth = Math.Max(1, maxWidth),
            TextAlignment = paragraph.Align switch
            {
                TextAlign.Center => TextAlignment.Center,
                TextAlign.Right => TextAlignment.End,
                TextAlign.Justify => TextAlignment.Justify,
                _ => TextAlignment.Start,
            },
        };

    public FlowDirection Flow(TextParagraph paragraph) =>
        paragraph.Direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    public bool RightToLeft(TextParagraph paragraph) => paragraph.Direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft;
}

public sealed class RunStyler(ITextTheme theme, ParagraphTypesetter typesetter, IAsianScriptRanges scripts, IHyperlinkAppearance links)
{
    public RunStyler(ITextTheme theme, ParagraphTypesetter typesetter) : this(theme, typesetter, new AsianScriptRanges(), new HyperlinkAppearance())
    {
    }

    public void Apply(FormattedText formatted, string text, TextRun run, TextParagraph paragraph, int start, int length)
    {
        var baselineScale = run.Baseline == TextBaseline.Normal ? 1.0 : 0.66;
        formatted.SetFontSize(typesetter.Em((run.FontSize ?? paragraph.FontSize) * baselineScale), start, length);
        formatted.SetFontWeight(run.Bold ? FontWeight.SemiBold : FontWeight.Normal, start, length);
        formatted.SetFontStyle(run.Italic ? FontStyle.Italic : FontStyle.Normal, start, length);

        var family = run.Font ?? (run.MajorFont ? theme.MajorFont : theme.MinorFont);
        formatted.SetFontFamily(new FontFamily($"{family}, Inter, Helvetica, Arial"), start, length);
        if (run.AsianFont is { Length: > 0 } asian)
            foreach (var span in scripts.Split(text, start, length).Where(span => span.Asian))
                formatted.SetFontFamily(new FontFamily($"{asian}, {family}, Inter, Helvetica, Arial"), span.Start, span.Length);
        formatted.SetForegroundBrush(new SolidColorBrush(
            Color.FromUInt32(links.Ink(run, theme))), start, length);

        if (Decorations(run) is { Count: > 0 } decorations) formatted.SetTextDecorations(decorations, start, length);
    }

    private TextDecorationCollection Decorations(TextRun run)
    {
        var decorations = new TextDecorationCollection();
        if (links.Underlined(run)) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Underline });
        if (run.Strikethrough) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Strikethrough });
        if (run.Overline) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Overline });
        return decorations;
    }
}

public sealed class SpellingWaves
{
    private readonly IPen _ink = new Pen(new SolidColorBrush(Color.FromRgb(220, 38, 38)), 1.2);

    private readonly IPen _grammar = new Pen(new SolidColorBrush(Color.FromRgb(37, 99, 235)), 1.2);

    public void Paint(
        DrawingContext context, FormattedText formatted, Point origin, int offset,
        IReadOnlyList<PulseOffice.Text.Spelling.WordSpan> words, double scale)
    {
        foreach (var word in words)
        {
            var area = formatted.BuildHighlightGeometry(origin, offset + word.Start, word.Length);
            if (area is null || area.Bounds.Width <= 0) continue;

            PaintWave(context, area.Bounds, scale);
        }
    }

    public void PaintWave(DrawingContext context, Rect bounds, double scale) => PaintWave(context, bounds, scale, false);

    public void PaintWave(DrawingContext context, Rect bounds, double scale, bool grammar)
    {
        var step = Math.Max(2.0, 60 * scale);
        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            var baseY = bounds.Bottom - step / 2;
            figure.BeginFigure(new Point(bounds.Left, baseY), false);
            var up = true;
            for (var x = bounds.Left + step; x <= bounds.Right + step / 2; x += step)
            {
                figure.LineTo(new Point(Math.Min(x, bounds.Right), up ? baseY - step / 2 : baseY));
                up = !up;
            }
            figure.EndFigure(false);
        }
        context.DrawGeometry(null, grammar ? _grammar : _ink, geometry);
    }
}

public sealed record TypesetSegment(FormattedText Text, double X, double Y, int Start, int End);

public sealed record TypesetBulletPicture(ListBulletPicture Picture, Rect Box)
{
    public double Drop { get; init; }
}

public sealed record TypesetLine(
    IReadOnlyList<TypesetSegment> Segments, FormattedText? Label, double LabelX, double Before, double After, int Offset, TextParagraph Paragraph)
{
    public TypesetBulletPicture? Picture { get; init; }

    public double Height => Math.Max(Segments.Count == 0 ? 0 : Segments.Max(segment => segment.Y + segment.Text.Height), Picture?.Box.Bottom ?? 0);

    public double Advance => Before + Height * Paragraph.LineSpacing + After;
}

public sealed record TextFrameArea(Rect Inner, double Scale)
{
    public double Points(double points) => points * ParagraphTypesetter.PointToTwip * Scale;
}

public sealed class TextFrameGeometry
{
    public TextFrameArea Area(Rect bounds, TextFrame frame, double scale)
    {
        var insets = frame.Insets;
        var k = ParagraphTypesetter.PointToTwip * scale;
        var left = Math.Min(insets.Left * k, bounds.Width / 2);
        var right = Math.Min(insets.Right * k, bounds.Width / 2);
        var top = Math.Min(insets.Top * k, bounds.Height / 2);
        var bottom = Math.Min(insets.Bottom * k, bounds.Height / 2);
        return new TextFrameArea(new Rect(bounds.X + left, bounds.Y + top,
            Math.Max(1, bounds.Width - left - right), Math.Max(1, bounds.Height - top - bottom)), scale);
    }
}

public sealed class StyledParagraph(
    string content, int inline, IReadOnlyList<TextRun> runs, TextParagraph paragraph,
    ParagraphTypesetter typesetter, RunStyler styler, RunText runText, TextFields fields)
{
    public string Content => content;

    public FormattedText Build(int start, int end, double maxWidth)
    {
        var piece = content[start..end];
        var formatted = typesetter.Typeset(piece.Length == 0 ? " " : piece, paragraph, typesetter.Ink(paragraph), maxWidth, runText.Culture(RunAt(start), fields));
        if (double.IsPositiveInfinity(maxWidth)) formatted.TextAlignment = TextAlignment.Start;
        var position = inline;
        foreach (var run in runs)
        {
            var shown = runText.Shown(run, fields);
            if (shown.Length == 0) continue;
            var from = Math.Max(position, start);
            var to = Math.Min(position + shown.Length, end);
            if (to > from) styler.Apply(formatted, piece, run, paragraph, from - start, to - from);
            position += shown.Length;
        }
        return formatted;
    }

    private TextRun? RunAt(int start)
    {
        var position = inline;
        foreach (var run in runs)
        {
            position += runText.Shown(run, fields).Length;
            if (position > start) return run;
        }
        return runs.Count > 0 ? runs[^1] : null;
    }
}

public sealed class TextBlockTypesetter(
    ITextTheme theme, IListLabels labels, RunText runText, IFirstLineBreaker breaker, IParagraphLineBreaker lines, ILineJustifier justifier, ITabLayout tabs, IBulletPictureSizing pictures)
{
    private const string PictureSpace = "\u2003";

    private readonly PulseOffice.Text.Rich.Bidi.AlignMirroring _aligns = new();

    public TextBlockTypesetter(ITextTheme theme, IListLabels labels, RunText runText)
        : this(theme, labels, runText, new FirstLineBreaker(), new ParagraphLineBreaker(), new LineJustifier(), new TabLayout(), new BulletPictureSizing())
    {
    }

    public IReadOnlyList<TypesetLine> Typeset(
        IReadOnlyList<TextParagraph> paragraphs, TextFrameArea area, TextFields fields, double fontFactor, bool wrap = true)
    {
        var typesetter = new ParagraphTypesetter(theme, area.Scale * fontFactor);
        var styler = new RunStyler(theme, typesetter);
        var marks = labels.For(paragraphs);
        var result = new List<TypesetLine>(paragraphs.Count);

        for (var index = 0; index < paragraphs.Count; index++)
        {
            var paragraph = paragraphs[index];
            var startMargin = area.Points(paragraph.MarginLeft ?? paragraph.BulletLevel * 18);
            var endMargin = area.Points(paragraph.MarginRight ?? 0);
            var mirrored = typesetter.RightToLeft(paragraph);
            var margin = mirrored ? endMargin : startMargin;
            var indent = area.Points(paragraph.Indent ?? 0);
            var runs = paragraph.EffectiveRuns();
            var label = runs.All(run => runText.Shown(run, fields).Length == 0) ? string.Empty : marks[index];
            var picture = label.Length > 0 && paragraph is { Bullet: true, Numbered: false, BulletSymbol.Picture: { Data.Length: > 0 } chosen } ? chosen : null;
            var hanging = label.Length > 0 && indent < 0;
            var inline = label.Length > 0 && !hanging ? (picture is null ? label : PictureSpace) + "  " : string.Empty;
            var content = inline + string.Concat(runs.Select(run => runText.Shown(run, fields)));
            var styled = new StyledParagraph(content, inline.Length, runs, paragraph, typesetter, styler, runText, fields);

            var width = Math.Max(1, area.Inner.Width - (mirrored ? startMargin : endMargin));
            var segments = content.Contains('\t')
                ? Tabbed(styled, width, margin, hanging ? margin : margin + Math.Max(0, indent), wrap, paragraph.TabStops, area)
                : !wrap
                    ? Unwrapped(styled, width, margin + (hanging ? 0 : Math.Max(0, indent)), _aligns.Physical(paragraph.Align, paragraph.Direction))
                    : paragraph.Align == TextAlign.Justify
                        ? Justified(styled, width, margin, hanging ? margin : margin + Math.Max(0, indent))
                        : Wrapped(styled, width, margin, hanging ? 0 : indent, hanging);

            var labelText = hanging ? typesetter.Typeset(label, paragraph, typesetter.Ink(paragraph), Math.Max(1, -indent), runText.Culture(runs.FirstOrDefault(), fields)) : null;
            if (labelText is not null && runs.FirstOrDefault() is { } first) styler.Apply(labelText, label, Bulleted(first with { Underline = false, DoubleUnderline = false, Strikethrough = false, Hyperlink = null }, paragraph), paragraph, 0, label.Length);

            var labelX = hanging ? Math.Max(0, margin + indent) : margin + Math.Max(0, indent);
            var placed = picture is null ? null : Placed(picture, paragraph, runs, typesetter, styled.Build(0, Math.Min(1, content.Length), double.PositiveInfinity), segments.Count > 0 ? segments[0].Text.Baseline : 0, labelX);
            if (placed is not null && placed.Drop > 0)
                segments = segments.Select(segment => segment with { Y = segment.Y + placed.Drop }).ToList();
            result.Add(new TypesetLine(segments, placed is null ? labelText : null, Math.Max(0, margin + indent),
                typesetter.Points(paragraph.SpaceBefore), typesetter.Points(paragraph.SpaceAfter) + 2 * area.Scale, inline.Length, paragraph)
            {
                Picture = placed,
            });
        }

        return result;
    }

    public double Height(IReadOnlyList<TypesetLine> lines) => lines.Sum(line => line.Advance);

    private TypesetBulletPicture Placed(ListBulletPicture picture, TextParagraph paragraph, IReadOnlyList<TextRun> runs, ParagraphTypesetter typesetter, FormattedText line, double baseline, double x)
    {
        var points = (runs.FirstOrDefault()?.FontSize ?? paragraph.FontSize) * (paragraph.BulletSymbol?.RelativeSize ?? 1);
        var sized = pictures.Size(picture, points);
        var width = typesetter.Points(sized.Width);
        var height = typesetter.Points(sized.Height);
        return height > baseline
            ? new TypesetBulletPicture(picture, new Rect(x, 0, width, height)) { Drop = height - baseline }
            : new TypesetBulletPicture(picture, new Rect(x, Math.Max(0, (line.Height - height) / 2), width, height));
    }

    private TextRun Bulleted(TextRun run, TextParagraph paragraph) =>
        paragraph is { Bullet: true, Numbered: false, BulletSymbol: { } symbol }
            ? run with { Color = symbol.Color ?? run.Color, FontSize = symbol.RelativeSize is { } scale ? run.FontSize * scale : run.FontSize }
            : run;

    private IReadOnlyList<TypesetSegment> Wrapped(StyledParagraph styled, double width, double margin, double indent, bool hanging)
    {
        var content = styled.Content;
        var textX = hanging ? margin : margin + Math.Max(0, indent);
        if (hanging || indent <= 0 || content.Length == 0)
            return [new TypesetSegment(styled.Build(0, content.Length, width - textX), textX, 0, 0, content.Length)];

        var firstWidth = Math.Max(1, width - textX);
        var split = breaker.Break(content, firstWidth, end => styled.Build(0, end, double.PositiveInfinity).Width);
        var first = styled.Build(0, split.End, firstWidth);
        if (split.Next >= content.Length)
            return [new TypesetSegment(first, textX, 0, 0, split.End)];

        var rest = styled.Build(split.Next, content.Length, width - margin);
        return
        [
            new TypesetSegment(first, textX, 0, 0, split.End),
            new TypesetSegment(rest, margin, first.Height, split.Next, content.Length),
        ];
    }

    private IReadOnlyList<TypesetSegment> Justified(StyledParagraph styled, double width, double margin, double firstX)
    {
        var content = styled.Content;
        double Natural(int start, int end) => end <= start ? 0 : styled.Build(start, end, double.PositiveInfinity).Width;
        double Advance(int start, int end) => end <= start ? 0 : styled.Build(start, end, double.PositiveInfinity).WidthIncludingTrailingWhitespace;

        var broken = lines.Break(content, Math.Max(1, width - firstX), Math.Max(1, width - margin), Natural);
        var segments = new List<TypesetSegment>();
        var y = 0.0;
        foreach (var line in broken)
        {
            var x = line.Start == 0 ? firstX : margin;
            var available = Math.Max(1, width - x);
            var height = 0.0;
            foreach (var piece in justifier.Spread(content, line, available, Advance))
            {
                var formatted = styled.Build(piece.Start, piece.End, double.PositiveInfinity);
                segments.Add(new TypesetSegment(formatted, x + piece.X, y, piece.Start, piece.End));
                height = Math.Max(height, formatted.Height);
            }
            y += height;
        }
        return segments;
    }

    private IReadOnlyList<TypesetSegment> Tabbed(StyledParagraph styled, double width, double margin, double firstX, bool wrap, IReadOnlyList<TabStop>? stops, TextFrameArea area)
    {
        var content = styled.Content;
        var scaled = stops?.Select(stop => stop with { Position = area.Points(stop.Position) }).ToList();
        var interval = area.Points(TabLayout.DefaultIntervalPoints);
        double Natural(int start, int end) => end <= start ? 0 : styled.Build(start, end, double.PositiveInfinity).WidthIncludingTrailingWhitespace;
        double Expanded(int start, int end)
        {
            var placed = tabs.Place(content, start, end, margin, margin, scaled, interval, Natural);
            return placed.Count == 0 ? 0 : placed[^1].X + Natural(placed[^1].Start, placed[^1].End) - margin;
        }

        var broken = wrap
            ? lines.Break(content, Math.Max(1, width - firstX), Math.Max(1, width - margin), Expanded)
            : [new BrokenLine(0, content.Length, content.Length, true)];
        var segments = new List<TypesetSegment>();
        var y = 0.0;
        foreach (var line in broken)
        {
            var height = styled.Build(line.Start, line.Start, double.PositiveInfinity).Height;
            foreach (var piece in tabs.Place(content, line.Start, line.End, margin, line.Start == 0 ? firstX : margin, scaled, interval, Natural))
            {
                var formatted = styled.Build(piece.Start, piece.End, double.PositiveInfinity);
                segments.Add(new TypesetSegment(formatted, piece.X, y, piece.Start, piece.End));
                height = Math.Max(height, formatted.Height);
            }
            y += height;
        }
        return segments;
    }

    private IReadOnlyList<TypesetSegment> Unwrapped(StyledParagraph styled, double width, double textX, TextAlign align)
    {
        var formatted = styled.Build(0, styled.Content.Length, double.PositiveInfinity);
        var natural = Math.Max(1, formatted.WidthIncludingTrailingWhitespace);
        formatted.MaxTextWidth = natural + 1;
        var free = width - textX - natural;
        var x = textX + align switch
        {
            TextAlign.Center => free / 2,
            TextAlign.Right => free,
            _ => 0,
        };
        return [new TypesetSegment(formatted, x, 0, 0, styled.Content.Length)];
    }
}

public sealed class TextFramePlacement
{
    public IReadOnlyList<double> Tops(IReadOnlyList<TypesetLine> lines, TextFrameArea area, TextAnchor anchor, double height)
    {
        var free = area.Inner.Height - height;
        var gap = anchor == TextAnchor.Justified && lines.Count > 1 && free > 0 ? free / (lines.Count - 1) : 0;
        var y = area.Inner.Y + anchor switch
        {
            TextAnchor.Middle => free / 2,
            TextAnchor.Bottom => free,
            TextAnchor.Justified when lines.Count <= 1 => free / 2,
            _ => 0,
        };

        var tops = new List<double>(lines.Count);
        foreach (var line in lines)
        {
            y += line.Before;
            tops.Add(y);
            y += line.Height * line.Paragraph.LineSpacing + line.After + gap;
        }
        return tops;
    }
}

public interface ITextFrameMeasure
{
    double Height(RichText text, double width, ITextTheme theme, TextFields fields);
}

public sealed class TextFrameMeasure(IListLabels labels) : ITextFrameMeasure
{
    private readonly RunText _runText = new();

    public double Height(RichText text, double width, ITextTheme theme, TextFields fields)
    {
        var frame = text.Frame;
        var inner = Math.Max(1, width - frame.Insets.Horizontal * ParagraphTypesetter.PointToTwip);
        var area = new TextFrameArea(new Rect(0, 0, inner, double.MaxValue / 4), 1.0);
        var block = new TextBlockTypesetter(theme, labels, _runText);
        var paragraphs = text.Paragraphs.Count > 0 ? text.Paragraphs : [new TextParagraph(string.Empty)];
        var lines = block.Typeset(paragraphs, area, fields, 1.0, frame.WordWrap);
        return block.Height(lines) + frame.Insets.Vertical * ParagraphTypesetter.PointToTwip;
    }
}

