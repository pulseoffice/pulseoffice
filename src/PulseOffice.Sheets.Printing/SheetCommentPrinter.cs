using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Printing;

public interface ISheetCommentPrinter
{
    IReadOnlyList<IReadOnlyList<string>> EndPages(Worksheet sheet, CellRange? area, DocumentRect content);

    void PaintEndPage(IPageCanvas canvas, IReadOnlyList<string> lines, DocumentRect content);

    void PaintDisplayed(IPageCanvas canvas, Worksheet sheet, IReadOnlyDictionary<int, Twip> rowTops, IReadOnlyDictionary<int, Twip> columnLefts);
}

public sealed class SheetCommentPrinter : ISheetCommentPrinter
{
    private static readonly PrintFont Font = new("Helvetica", 9);
    private static readonly Twip LineHeight = Twip.FromPoints(12);
    private static readonly Twip Padding = Twip.FromPoints(3);
    private static readonly PrintColor Ink = new(17, 24, 39);
    private static readonly PrintColor Fill = new(255, 255, 225);
    private static readonly PrintColor Edge = new(128, 128, 128);

    private readonly CommentBoxGeometry _geometry = new();

    public IReadOnlyList<IReadOnlyList<string>> EndPages(Worksheet sheet, CellRange? area, DocumentRect content)
    {
        var lines = new List<string>();
        foreach (var (address, comment) in sheet.Comments.OrderBy(pair => pair.Key.Row).ThenBy(pair => pair.Key.Column))
        {
            if (area is { } range && !range.Contains(address)) continue;
            if (lines.Count > 0) lines.Add(string.Empty);
            lines.Add(address.ToA1());
            lines.AddRange(Lines(comment.Author, comment.Text).Select(line => "    " + line));
            foreach (var reply in comment.Replies) lines.AddRange(Lines(reply.Author, reply.Text).Select(line => "        " + line));
        }

        var perPage = Math.Max((int)(content.Height.Value / LineHeight.Value), 1);
        return [.. lines.Chunk(perPage).Select(chunk => (IReadOnlyList<string>)chunk)];
    }

    public void PaintEndPage(IPageCanvas canvas, IReadOnlyList<string> lines, DocumentRect content)
    {
        var y = content.Top;
        foreach (var line in lines)
        {
            if (line.Length > 0) canvas.DrawText(line, new DocumentRect(content.Left, y, content.Width, LineHeight), Font, Ink, baseline: TextBaseline.Top);
            y += LineHeight;
        }
    }

    public void PaintDisplayed(IPageCanvas canvas, Worksheet sheet, IReadOnlyDictionary<int, Twip> rowTops, IReadOnlyDictionary<int, Twip> columnLefts)
    {
        foreach (var (address, comment) in sheet.Comments)
        {
            if (!comment.IsAlwaysVisible || !rowTops.TryGetValue(address.Row, out var top) || !columnLefts.TryGetValue(address.Column, out var left)) continue;

            var box = comment.Box ?? CommentBox.Default;
            var cellRight = left + sheet.Layout.ColumnWidth(address.Column);
            var bounds = new DocumentRect(
                cellRight + Twip.FromPoints(box.OffsetX), Max(top + Twip.FromPoints(box.OffsetY), top),
                Twip.FromPoints(box.Width), Twip.FromPoints(box.Height));

            canvas.DrawLine(new DocumentPoint(cellRight, top), new DocumentPoint(bounds.Left, bounds.Top), Edge, Twip.FromPoints(0.5));
            canvas.FillRectangle(bounds, Fill);
            canvas.DrawLine(new DocumentPoint(bounds.Left, bounds.Top), new DocumentPoint(bounds.Right, bounds.Top), Edge, Twip.FromPoints(0.5));
            canvas.DrawLine(new DocumentPoint(bounds.Right, bounds.Top), new DocumentPoint(bounds.Right, bounds.Bottom), Edge, Twip.FromPoints(0.5));
            canvas.DrawLine(new DocumentPoint(bounds.Right, bounds.Bottom), new DocumentPoint(bounds.Left, bounds.Bottom), Edge, Twip.FromPoints(0.5));
            canvas.DrawLine(new DocumentPoint(bounds.Left, bounds.Bottom), new DocumentPoint(bounds.Left, bounds.Top), Edge, Twip.FromPoints(0.5));

            using (canvas.Clip(bounds))
            {
                var y = bounds.Top + Padding;
                foreach (var line in Lines(comment.Author, comment.Text).Concat(comment.Replies.SelectMany(reply => Lines(reply.Author, reply.Text))))
                {
                    if ((y + LineHeight).Value > bounds.Bottom.Value) break;
                    canvas.DrawText(line, new DocumentRect(bounds.Left + Padding, y, bounds.Width - Padding * 2, LineHeight), Font, Ink, baseline: TextBaseline.Top);
                    y += LineHeight;
                }
            }
        }
    }

    private Twip Max(Twip first, Twip second) => first.Value >= second.Value ? first : second;

    private IEnumerable<string> Lines(string? author, string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (author is { Length: > 0 }) lines[0] = author + ": " + lines[0];
        return lines;
    }
}
