using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Text.Rich;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxHeaderFooterExtent(IStyleResolver styles)
{
    private const double DefaultFontSize = 12;

    public int Of(TextDocument document, HeaderFooterContent content)
    {
        if (!content.Enabled) return 0;
        if (content.Spacing.Value == 0) return content.MinimumHeight.Value;
        var estimated = (int)Math.Round(content.Default.Blocks.OfType<ParagraphBlock>().Sum(paragraph => Height(document, paragraph)) * ((double)PulseOffice.Foundation.Units.Twip.PerPoint));
        return Math.Max(content.MinimumHeight.Value, estimated) + content.Spacing.Value;
    }

    private double Height(TextDocument document, ParagraphBlock block)
    {
        var paragraph = styles.EffectiveParagraph(document.Styles, block.Content);
        var run = styles.EffectiveRun(document.Styles, block.Content, block.Content.Runs.FirstOrDefault() ?? new TextRun(string.Empty));
        return (run.FontSize ?? DefaultFontSize) * paragraph.LineSpacing + paragraph.SpaceBefore + paragraph.SpaceAfter;
    }
}
