using System.Globalization;
using DocumentFormat.OpenXml;
using PulseOffice.Text.Rich;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal sealed class DrawingRunReader(IDrawingFieldTypes fields, LevelStyleReader styles, IDrawingColorReader colors)
{
    private readonly PptxRunLanguages _languages = new();

    public TextRun? Read(OpenXmlElement element, LevelStyle inherited, IDrawingRunLinks links) => element switch
    {
        D.Run run => Read(run, inherited) with { Hyperlink = links.Read(run.RunProperties?.GetFirstChild<D.HyperlinkOnClick>()) },
        D.Break line => Read(new D.Run(
            line.RunProperties?.CloneNode(true) ?? new D.RunProperties(),
            new D.Text("\n")), inherited),
        D.Field field => Read(new D.Run(
            field.RunProperties?.CloneNode(true) ?? new D.RunProperties(),
            new D.Text(field.Text?.Text ?? string.Empty)), inherited) with { Hyperlink = links.Read(field.RunProperties?.GetFirstChild<D.HyperlinkOnClick>()), Field = fields.KindOf(field.Type?.Value), FieldDetail = fields.DetailOf(field.Type?.Value) },
        _ => null,
    };

    public bool IsDistinct(TextRun run) =>
        run.Underline || run.Strikethrough || run.Baseline != TextBaseline.Normal || run.Field != TextFieldKind.None
        || run.DoubleUnderline || run.SmallCaps || run.Outline || run.Font is not null || run.Highlight is not null
        || run.AsianFont is not null || run.Language is not null || run.AsianLanguage is not null || run.Hyperlink is not null;

    public TextRun Blank(D.EndParagraphRunProperties? end, LevelStyle inherited)
    {
        var style = styles.Character(end).Over(inherited);
        return new TextRun(string.Empty)
        {
            FontSize = style.FontSize,
            Bold = style.Bold ?? false,
            Italic = style.Italic ?? false,
            Color = style.Color,
            ColorRole = style.Color is null ? style.ColorRole : null,
            Font = style.ExplicitFont,
            MajorFont = style.IsMajorFont(false),
        };
    }

    private TextRun Read(D.Run run, LevelStyle inherited)
    {
        var properties = run.RunProperties;
        var style = styles.Character(properties).Over(inherited);
        var (western, asian) = _languages.Read(properties?.Language?.Value, properties?.AlternativeLanguage?.Value);
        var eastAsianFont = properties?.GetFirstChild<D.EastAsianFont>()?.Typeface?.Value;
        return new TextRun(run.Text?.Text ?? string.Empty)
        {
            FontSize = style.FontSize,
            Bold = style.Bold ?? false,
            Italic = style.Italic ?? false,
            Underline = Raw(properties, "u") is { } underline && underline is not "none" and not "dbl",
            DoubleUnderline = Raw(properties, "u") == "dbl",
            SmallCaps = Raw(properties, "cap") == "small",
            Outline = properties?.GetFirstChild<D.Outline>() is not null,
            Strikethrough = Raw(properties, "strike") is "sngStrike" or "dblStrike",
            Highlight = colors.Read(properties?.GetFirstChild<D.Highlight>()),
            Baseline = int.TryParse(Raw(properties, "baseline"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var baseline)
                ? baseline switch { > 0 => TextBaseline.Superscript, < 0 => TextBaseline.Subscript, _ => TextBaseline.Normal }
                : Raw(properties, "baseline") is { } percent && percent.EndsWith('%') && double.TryParse(percent[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var shift)
                    ? shift switch { > 0 => TextBaseline.Superscript, < 0 => TextBaseline.Subscript, _ => TextBaseline.Normal }
                    : TextBaseline.Normal,
            Color = style.Color,
            ColorRole = style.Color is null ? style.ColorRole : null,
            Font = style.ExplicitFont,
            MajorFont = style.IsMajorFont(false),
            AsianFont = eastAsianFont is { Length: > 0 } && !eastAsianFont.StartsWith('+') ? eastAsianFont : null,
            Language = western,
            AsianLanguage = asian,
        };
    }

    private string? Raw(OpenXmlElement? element, string name) =>
        element?.GetAttributes().FirstOrDefault(attribute => attribute.LocalName == name && string.IsNullOrEmpty(attribute.NamespaceUri)).Value;
}

internal sealed class DrawingParagraphReader(DrawingRunReader runs, LevelStyleReader styles, DrawingParagraphLevels levels)
{
    public IReadOnlyList<TextParagraph> Read(OpenXmlElement? body, ITextStyleLayer inherited, bool listDefault, IDrawingRunLinks links)
    {
        var paragraphs = body?.Elements<D.Paragraph>().Select(p => Read(p, inherited, listDefault, links)).ToList() ?? [];
        return paragraphs.Count > 0 ? paragraphs : [new TextParagraph(string.Empty)];
    }

    private readonly PulseOffice.Text.Rich.Bidi.AlignMirroring _aligns = new();

    private TextParagraph Read(D.Paragraph paragraph, ITextStyleLayer inherited, bool listDefault, IDrawingRunLinks links)
    {
        var properties = paragraph.ParagraphProperties;
        var level = levels.Read(properties);
        var style = styles.Paragraph(properties).Over(inherited.At(level));
        var read = paragraph.ChildElements.Select(element => runs.Read(element, style, links)).OfType<TextRun>().ToList();
        var first = read.Count > 0 ? read[0] : runs.Blank(paragraph.GetFirstChild<D.EndParagraphRunProperties>(), style);

        var numbered = style.Numbered ?? false;
        var bullet = !numbered && (style.Bullet ?? listDefault);

        return new TextParagraph(string.Concat(read.Select(r => r.Text)))
        {
            FontSize = first.FontSize ?? 18,
            Bold = first.Bold,
            Italic = first.Italic,
            Underline = first.Underline,
            Strikethrough = first.Strikethrough,
            Baseline = first.Baseline,
            Color = first.Color,
            ColorRole = first.ColorRole,
            MajorFont = first.MajorFont,
            Align = _aligns.Physical(style.Align ?? TextAlign.Left, style.Direction),
            Direction = style.Direction,
            LineSpacing = style.LineSpacing ?? 1.0,
            BulletLevel = level,
            Bullet = bullet,
            BulletSymbol = bullet ? style.Symbol : null,
            Numbered = numbered,
            NumberFormat = numbered ? style.NumberFormat : null,
            SpaceBefore = style.SpaceBefore ?? 0,
            SpaceAfter = style.SpaceAfter ?? 0,
            MarginLeft = style.Direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft ? style.MarginRight : style.MarginLeft,
            Indent = style.Indent,
            MarginRight = style.Direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft ? style.MarginLeft : style.MarginRight,
            TabStops = style.TabStops,
            Runs = read.Count > 1 || (read.Count == 1 && runs.IsDistinct(read[0])) ? read : [],
        };
    }
}
