using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxListHeaderIndent(IStyleResolver styles)
{
    public bool IsHeader(TextParagraph paragraph, StyleSheet sheet) =>
        paragraph.ListHeader && List(paragraph, sheet) is not null;

    public TextParagraph Apply(TextParagraph paragraph, StyleSheet sheet)
    {
        if (!paragraph.ListHeader || List(paragraph, sheet) is not { } list) return paragraph;
        var level = list.Level(paragraph.BulletLevel);
        var margin = styles.EffectiveParagraph(sheet, paragraph).MarginLeft ?? 0;
        return paragraph with { MarginLeft = margin + level.Indent + level.Hanging };
    }

    private ListStyle? List(TextParagraph paragraph, StyleSheet sheet) =>
        sheet.List.Find(paragraph.ListStyleName ?? styles.ResolveParagraphStyle(sheet, paragraph.StyleName).ListStyleName) is { Levels.Count: > 0 } list ? list : null;
}
