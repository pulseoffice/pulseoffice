using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed record DocxParagraphTabs(ParagraphProperties Properties, IReadOnlyList<double> Cleared);

public sealed class DocxTabPositions(IStyleResolver styles)
{
    private const double Tolerance = 0.01;

    public ParagraphProperties Defaults(ParagraphProperties defaults, StyleSheet sheet) =>
        Shifted(defaults, sheet, defaults.MarginLeft ?? 0);

    public ParagraphProperties Style(ParagraphStyle style, StyleSheet sheet) =>
        Shifted(style.Paragraph, sheet, styles.ResolveParagraphStyle(sheet, style.Name).Paragraph.MarginLeft ?? 0);

    public DocxParagraphTabs Paragraph(ParagraphProperties direct, TextParagraph paragraph, StyleSheet sheet)
    {
        if (!sheet.TabsRelativeToIndent) return new DocxParagraphTabs(direct, []);
        var style = styles.ResolveParagraphStyle(sheet, paragraph.StyleName).Paragraph;
        var styleMargin = style.MarginLeft ?? 0;
        var margin = (styles.EffectiveParagraph(sheet, paragraph).MarginLeft ?? 0) + ListExtra(paragraph, sheet);
        var inherited = style.TabStops ?? [];
        if (direct.TabStops is null && (inherited.Count == 0 || Math.Abs(margin - styleMargin) < Tolerance)) return new DocxParagraphTabs(direct, []);
        var written = Shift(direct.TabStops ?? inherited, margin);
        var cleared = inherited.Select(tab => tab.Position + styleMargin)
            .Where(position => written.All(tab => Math.Abs(tab.Position - position) > Tolerance))
            .ToList();
        return new DocxParagraphTabs(direct with { TabStops = written }, cleared);
    }

    private double ListExtra(TextParagraph paragraph, StyleSheet sheet)
    {
        if (paragraph.ListHeader) return 0;
        var name = paragraph.ListStyleName ?? styles.ResolveParagraphStyle(sheet, paragraph.StyleName).ListStyleName;
        if (sheet.List.Find(name) is not { Levels.Count: > 0 } list) return 0;
        var level = list.Level(Math.Clamp(paragraph.BulletLevel, 0, list.Levels.Count - 1));
        return level.Indent + level.Hanging;
    }

    private ParagraphProperties Shifted(ParagraphProperties properties, StyleSheet sheet, double margin) =>
        sheet.TabsRelativeToIndent && properties.TabStops is { Count: > 0 } stops && margin != 0
            ? properties with { TabStops = Shift(stops, margin) }
            : properties;

    private IReadOnlyList<TabStop> Shift(IReadOnlyList<TabStop> stops, double margin) =>
        [.. stops.Select(stop => stop with { Position = stop.Position + margin })];
}
