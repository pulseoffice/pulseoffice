using PulseOffice.Foundation.Colors;
using DocumentFormat.OpenXml;
using PulseOffice.Text.Rich;
using D = DocumentFormat.OpenXml.Drawing;

namespace PulseOffice.Formats.OpenXml.DrawingML;

internal sealed class RunPropertiesBuilder(OoxmlWriteUnits units, ITextScripts scripts, PptxRunLanguages languages)
{
    public RunPropertiesBuilder(OoxmlWriteUnits units) : this(units, new TextScripts(), new PptxRunLanguages())
    {
    }

    public D.RunProperties Build(TextRun run, ITextTheme theme, IDrawingRunLinks links)
    {
        var properties = new D.RunProperties
        {
            FontSize = (int)Math.Round((run.FontSize ?? 18) * 100),
            Bold = run.Bold,
            Italic = run.Italic,
        };

        if (run.DoubleUnderline) properties.Underline = D.TextUnderlineValues.Double;
        else if (run.Underline) properties.Underline = D.TextUnderlineValues.Single;
        if (run.Strikethrough) properties.Strike = D.TextStrikeValues.SingleStrike;
        if (run.SmallCaps) properties.Capital = D.TextCapsValues.Small;
        if (run.Baseline == TextBaseline.Superscript) properties.Baseline = 30000;
        else if (run.Baseline == TextBaseline.Subscript) properties.Baseline = -25000;

        var colour = units.Hex(run.Color ?? theme.Resolve(run.ColorRole ?? ColorRole.Text));
        properties.Append(new D.SolidFill(new D.RgbColorModelHex { Val = colour }));
        if (run.Highlight is { } highlight) properties.Append(new D.Highlight(new D.RgbColorModelHex { Val = units.Hex(highlight) }));
        properties.Append(new D.LatinFont { Typeface = run.Font ?? (run.MajorFont ? "+mj-lt" : "+mn-lt") });
        if (run.AsianFont is { Length: > 0 } asian) properties.Append(new D.EastAsianFont { Typeface = asian });
        var (lang, altLang) = languages.Write(run.Language, run.AsianLanguage, scripts.HasEastAsian(run.Text));
        if (lang is not null) properties.Language = lang;
        if (altLang is not null) properties.AlternativeLanguage = altLang;
        if (run.Outline) properties.InsertAt(new D.Outline(new D.SolidFill(new D.RgbColorModelHex { Val = colour })) { Width = 9525 }, 0);
        if (run.Hyperlink is { Length: > 0 } target && links.Build(target) is { } link) properties.Append(link);

        return properties;
    }
}

internal sealed class ParagraphBuilder(OoxmlWriteUnits units, RunPropertiesBuilder runs, IDrawingFieldTypes fields, IDrawingBulletPictures pictures)
{
    public ParagraphBuilder(OoxmlWriteUnits units, RunPropertiesBuilder runs, IDrawingFieldTypes fields) : this(units, runs, fields, new DrawingBulletPictures())
    {
    }

    private readonly ParagraphMargins _margins = new();
    private readonly PulseOffice.Text.Rich.Bidi.AlignMirroring _aligns = new();
    private readonly DrawingTabStops _tabs = new();
    private readonly DrawingListMarkers _markers = new(pictures);

    public D.Paragraph Build(TextParagraph paragraph, ITextTheme theme, IDrawingRunLinks links)
    {
        var mirrored = paragraph.Direction == PulseOffice.Text.Rich.Bidi.TextDirection.RightToLeft;
        var physical = mirrored ? paragraph with { MarginLeft = paragraph.MarginRight, MarginRight = paragraph.MarginLeft, Align = _aligns.Mirror(paragraph.Align) } : paragraph;
        var properties = new D.ParagraphProperties { Alignment = units.Align(physical.Align), Level = paragraph.BulletLevel };
        if (paragraph.Direction is { } direction) properties.RightToLeft = mirrored;
        if (_margins.Written(physical.MarginLeft, paragraph.Indent) is { } margin) properties.LeftMargin = (int)Math.Round(margin * 12700);
        if (paragraph.Indent is { } indent) properties.Indent = (int)Math.Round(indent * 12700);
        if (physical.MarginRight is { } right) properties.RightMargin = (int)Math.Round(right * 12700);

        if (Math.Abs(paragraph.LineSpacing - 1.0) > 0.001)
            properties.Append(new D.LineSpacing(new D.SpacingPercent { Val = (int)Math.Round(paragraph.LineSpacing * 100000) }));
        if (paragraph.SpaceBefore > 0)
            properties.Append(new D.SpaceBefore(new D.SpacingPoints { Val = (int)Math.Round(paragraph.SpaceBefore * 100) }));
        if (paragraph.SpaceAfter > 0)
            properties.Append(new D.SpaceAfter(new D.SpacingPoints { Val = (int)Math.Round(paragraph.SpaceAfter * 100) }));
        foreach (var element in _markers.Write(paragraph.Numbered, paragraph.Bullet, paragraph.BulletSymbol, paragraph.NumberFormat)) properties.Append(element);
        if (paragraph.TabStops is { Count: > 0 } stops) properties.Append(_tabs.Write(stops));

        var result = new D.Paragraph(properties);
        foreach (var run in paragraph.EffectiveRuns()) result.Append(Runs(run, theme, links));
        return result;
    }

    private IEnumerable<OpenXmlElement> Runs(TextRun run, ITextTheme theme, IDrawingRunLinks links)
    {
        if (run.Field != TextFieldKind.None || !run.Text.Contains('\n'))
        {
            yield return Run(run, theme, links);
            yield break;
        }

        var pieces = run.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < pieces.Length; index++)
        {
            if (index > 0) yield return new D.Break(runs.Build(run with { Hyperlink = null }, theme, links));
            if (pieces[index].Length > 0) yield return Run(run with { Text = pieces[index] }, theme, links);
        }
    }

    private OpenXmlElement Run(TextRun run, ITextTheme theme, IDrawingRunLinks links) =>
        run.Field == TextFieldKind.None
            ? new D.Run(runs.Build(run, theme, links), new D.Text(run.Text))
            : new D.Field(runs.Build(run, theme, links), new D.Text(run.Text))
            {
                Id = $"{{{Guid.NewGuid().ToString().ToUpperInvariant()}}}",
                Type = fields.TypeOf(run),
            };
}
