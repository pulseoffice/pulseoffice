using Avalonia;
using Avalonia.Media;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets.Addressing;
using PulseOffice.UI.Theming;

namespace PulseOffice.Sheets.UI.GridParts.Painting;

internal sealed class GridNotePainter(GridViewport viewport, IGridTheme theme)
{
    private const double Padding = 4;

    public Rect NoteBounds(CellAddress address, CellComment comment)
    {
        var cell = viewport.MergedRect(address);
        var box = comment.Box ?? CommentBox.Default;
        return new Rect(
            cell.Right + GridGeometry.ToPixels(Twip.FromPoints(box.OffsetX)),
            Math.Max(cell.Y + GridGeometry.ToPixels(Twip.FromPoints(box.OffsetY)), viewport.Geometry.ColumnHeaderHeight),
            GridGeometry.ToPixels(Twip.FromPoints(box.Width)),
            GridGeometry.ToPixels(Twip.FromPoints(box.Height)));
    }

    public void PaintNotes(DrawingContext context)
    {
        if (viewport.Sheet is not { } sheet || sheet.Comments.Count == 0) return;

        var area = viewport.GridArea();
        var fill = theme.Brush(Palette.Keys.SurfaceOverlay, Brushes.LightYellow);
        var ink = theme.Brush(Palette.Keys.TextPrimary, Brushes.Black);
        var border = new Pen(theme.Brush(Palette.Keys.BorderStrong, Brushes.Gray), 1);

        foreach (var (address, comment) in sheet.Comments)
        {
            if (!comment.IsAlwaysVisible) continue;
            var bounds = NoteBounds(address, comment);
            if (!bounds.Intersects(area)) continue;

            var cell = viewport.MergedRect(address);
            context.DrawLine(border, new Point(cell.Right, cell.Y), new Point(bounds.X, bounds.Y + Math.Min(bounds.Height, 10)));
            context.FillRectangle(fill, bounds);
            context.DrawRectangle(border, bounds);

            var text = new FormattedText(
                Text(comment), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 11, ink)
            {
                MaxTextWidth = Math.Max(bounds.Width - 2 * Padding, 1),
                MaxTextHeight = Math.Max(bounds.Height - 2 * Padding, 1),
            };
            using (context.PushClip(bounds))
                context.DrawText(text, new Point(bounds.X + Padding, bounds.Y + Padding));
        }
    }

    private string Text(CellComment comment) =>
        string.Join("\n", new[] { Line(comment.Author, comment.Text) }.Concat(comment.Replies.Select(reply => Line(reply.Author, reply.Text))));

    private string Line(string? author, string text) => author is { Length: > 0 } ? $"{author}: {text}" : text;
}
