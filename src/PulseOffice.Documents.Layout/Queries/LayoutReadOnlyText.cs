using System.Text;
using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.Layout.Queries;

public static class LayoutParagraphs
{
    public const string ContinuationNotice = "note-continuation-notice";
}

public sealed record LayoutText(int Page, string Text, IReadOnlyList<LayoutRect> Lines)
{
    public bool Contains(int page, double x, double y) =>
        page == Page && Lines.Any(line => line.Contains(x, y));
}

public interface ILayoutReadOnlyText
{
    bool IsReadOnly(LineBox line);

    LayoutText? At(DocumentLayout layout, int page, double x, double y);

    IReadOnlyList<LayoutText> OnPages(DocumentLayout layout, int first, int last);
}

public sealed class LayoutReadOnlyText : ILayoutReadOnlyText
{
    public bool IsReadOnly(LineBox line) =>
        string.Equals(line.ParagraphId, LayoutParagraphs.ContinuationNotice, StringComparison.Ordinal);

    public LayoutText? At(DocumentLayout layout, int page, double x, double y) =>
        OnPage(layout, page).FirstOrDefault(text => text.Contains(page, x, y));

    public IReadOnlyList<LayoutText> OnPages(DocumentLayout layout, int first, int last) =>
        [.. Enumerable.Range(Math.Max(0, first), Math.Max(0, Math.Min(last, layout.Pages.Count - 1) - Math.Max(0, first) + 1)).SelectMany(page => OnPage(layout, page))];

    private IEnumerable<LayoutText> OnPage(DocumentLayout layout, int page)
    {
        if (page < 0 || page >= layout.Pages.Count || layout.Pages[page].Footnotes is not { } area) yield break;
        foreach (var notice in Notices([.. area.Lines]))
            yield return new LayoutText(page, TextOf(notice), [.. notice.Select(line => line.Bounds)]);
    }

    private IEnumerable<List<LineBox>> Notices(IReadOnlyList<LineBox> lines)
    {
        var current = new List<LineBox>();
        foreach (var line in lines)
        {
            if (IsReadOnly(line) && (current.Count == 0 || line.Line.Index > current[^1].Line.Index))
            {
                current.Add(line);
                continue;
            }
            if (current.Count > 0) yield return current;
            current = IsReadOnly(line) ? [line] : [];
        }
        if (current.Count > 0) yield return current;
    }

    private string TextOf(IEnumerable<LineBox> lines)
    {
        var text = new StringBuilder();
        foreach (var run in lines.SelectMany(line => line.Line.Runs).Where(run => run.Kind is GlyphRunKind.Text or GlyphRunKind.Tab))
            text.Append(run.Kind == GlyphRunKind.Tab ? "\t" : run.Text);
        return text.ToString().Trim();
    }
}
