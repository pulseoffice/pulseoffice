using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;

namespace PulseOffice.Documents.Editing;

public interface IPageTextWidth
{
    int At(TextDocument document, string paragraphId);
}

public sealed class PageTextWidth : IPageTextWidth
{
    private const int DefaultTextWidth = 9638;

    public int At(TextDocument document, string paragraphId)
    {
        var cursor = new Cursor(document.InitialPageStyle);
        Walk(document.Body.Blocks, paragraphId, cursor, null);
        var page = document.Styles.Page.Find(cursor.Style) ?? document.Styles.Page.Find(document.InitialPageStyle);
        if (page is null) return DefaultTextWidth;
        var text = Math.Max(0, page.Width.Value - page.BoundMargins.Left.Value - page.Margins.Right.Value);
        return Column(text, cursor.Columns ?? page.Columns);
    }

    private void Walk(IEnumerable<Block> blocks, string paragraphId, Cursor cursor, SectionColumns? columns)
    {
        foreach (var block in blocks)
        {
            if (cursor.Found) return;
            switch (block)
            {
                case ParagraphBlock paragraph:
                    if (paragraph.Content.PageStyleName is { } style) cursor.Style = style;
                    if (paragraph.Id == paragraphId)
                    {
                        cursor.Found = true;
                        cursor.Columns = columns;
                    }
                    break;
                case TableBlock { PageStyleName: { } tableStyle }:
                    cursor.Style = tableStyle;
                    break;
                case SectionBlock section:
                    Walk(section.Content.Blocks, paragraphId, cursor, section.Columns.Count > 1 ? section.Columns : columns);
                    break;
            }
        }
    }

    private int Column(int text, SectionColumns columns)
    {
        if (columns.Count <= 1) return text;
        if (!columns.Uneven) return Math.Max(0, (text - columns.Gap.Value * (columns.Count - 1)) / columns.Count);
        var room = Math.Max(0, text - columns.Spans.Take(columns.Count - 1).Sum(span => span.Gap.Value));
        var weights = Math.Max(1L, columns.Spans.Sum(span => (long)Math.Max(0, span.Width.Value)));
        return (int)columns.Spans.Min(span => room * (long)Math.Max(0, span.Width.Value) / weights);
    }

    private sealed class Cursor(string style)
    {
        public string Style { get; set; } = style;

        public SectionColumns? Columns { get; set; }

        public bool Found { get; set; }
    }
}
