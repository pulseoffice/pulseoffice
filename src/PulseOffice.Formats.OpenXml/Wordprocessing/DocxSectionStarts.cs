using PulseOffice.Documents;
using PulseOffice.Documents.Styles;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxSectionStarts
{
    public W.SectionMarkValues? Mark(PageStyle style, TextDocument document)
    {
        if (style.Usage is not (PageUsage.Right or PageUsage.Left)) return null;
        if (style.NextStyleName is not { } next || next == style.Name) return null;
        if (document.Styles.Page.Find(next) is not { Usage: PageUsage.All } follow || !SameLayout(style, follow)) return null;
        return style.Usage == PageUsage.Right ? W.SectionMarkValues.OddPage : W.SectionMarkValues.EvenPage;
    }

    public PageUsage? Usage(W.SectionProperties section) =>
        section.GetFirstChild<W.SectionType>()?.Val?.Value is { } type
            ? type == W.SectionMarkValues.OddPage ? PageUsage.Right : type == W.SectionMarkValues.EvenPage ? PageUsage.Left : null
            : null;

    public string StarterName(string name, PageUsage usage) => name + (usage == PageUsage.Right ? "Right" : "Left");

    private bool SameLayout(PageStyle style, PageStyle follow) =>
        style.Size == follow.Size && style.Orientation == follow.Orientation && style.Margins == follow.Margins
        && style.Columns == follow.Columns && style.NumberFormat == follow.NumberFormat && style.Background == follow.Background;
}
