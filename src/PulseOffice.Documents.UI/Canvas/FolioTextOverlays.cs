using PulseOffice.Foundation.Zoom;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Layout.Queries;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.UI.Editing;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.UI.Controls.Text.Rich;

namespace PulseOffice.Documents.UI.Canvas;

internal sealed class FolioTextOverlays(FolioEditor editor, FolioCanvasServices services)
{
    public Dictionary<string, (int From, int To)> SelectionMap()
    {
        var map = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        if (editor.Preedit is not null) return map;
        if (editor.BlockRanges.Count > 0)
        {
            foreach (var block in editor.BlockRanges) map[block.Start.ParagraphId] = (block.Start.Offset, block.End.Offset);
            return map;
        }
        if (!editor.HasSelection) return map;
        var range = editor.Selection;
        foreach (var paragraph in services.Order.Between(editor.Document, range.Start, range.End))
        {
            var from = paragraph.Id == range.Start.ParagraphId ? range.Start.Offset : 0;
            var to = paragraph.Id == range.End.ParagraphId ? range.End.Offset : paragraph.Length + 1;
            map[paragraph.Id] = (from, to);
        }
        return map;
    }

    public void PaintSelection(DrawingContext context, PageBox page, Dictionary<string, (int From, int To)> selection, IBrush? accent)
    {
        if (selection.Count == 0) return;
        var brush = SelectionBrush(accent);
        foreach (var line in page.AllLines)
        {
            if (!selection.TryGetValue(line.ParagraphId, out var span)) continue;
            var composed = line.Line;
            var from = Math.Max(span.From, composed.Start);
            var to = Math.Min(span.To, composed.End);
            var tail = span.To > composed.End && composed.IsLast;
            if (to <= from && !tail) continue;
            var left = services.Geometry.XOf(line, from);
            var right = to > from ? services.Geometry.XOf(line, to) : left;
            if (tail) right += 4;
            using (Turn(context, page, line)) context.FillRectangle(brush, new Rect(left, line.Top, Math.Max(1, right - left), composed.CaretHeight));
        }
    }

    public void PaintFound(DrawingContext context, PageBox page, IBrush? accent)
    {
        var found = services.Found.Of(editor);
        if (found.Count == 0) return;
        var fill = accent is ISolidColorBrush solid ? new SolidColorBrush(solid.Color, 0.16) : SelectionBrush(accent);
        var frame = new Pen(accent ?? fill, 0.75);
        foreach (var line in page.AllLines)
        {
            if (!found.TryGetValue(line.ParagraphId, out var spans)) continue;
            var composed = line.Line;
            foreach (var (start, end) in spans)
            {
                var from = Math.Max(start, composed.Start);
                var to = Math.Min(end, composed.End);
                if (to <= from) continue;
                var left = services.Geometry.XOf(line, from);
                var right = services.Geometry.XOf(line, to);
                var box = new Rect(Math.Min(left, right), line.Top, Math.Max(1, Math.Abs(right - left)), composed.CaretHeight);
                using (Turn(context, page, line))
                {
                    context.FillRectangle(fill, box);
                    context.DrawRectangle(null, frame, box);
                }
            }
        }
    }

    public void PaintLayoutText(DrawingContext context, PageBox page, IBrush? accent)
    {
        var texts = editor.LayoutTexts.Where(text => text.Page == page.Index).ToList();
        if (texts.Count == 0) return;
        var brush = SelectionBrush(accent);
        foreach (var line in texts.SelectMany(text => text.Lines)) context.FillRectangle(brush, new Rect(line.X, line.Y, Math.Max(1, line.Width), line.Height));
    }

    private IBrush SelectionBrush(IBrush? accent) => new SolidColorBrush(accent is ISolidColorBrush solid ? solid.Color : Colors.DodgerBlue, 0.28);

    public bool Covers(PulseOffice.Documents.Commenting.CommentAnchor anchor, PulseOffice.Documents.Editing.DocumentPosition caret)
    {
        var order = editor.Services.Order;
        return order.Contains(editor.Document, anchor.Start.ParagraphId) && order.Contains(editor.Document, anchor.End.ParagraphId)
            && order.Compare(editor.Document, anchor.Start, caret) <= 0 && order.Compare(editor.Document, caret, anchor.End) <= 0;
    }

    internal sealed record ControlRange(int From, int To, bool Active, bool First, string? Title);

    private sealed record ControlRangeKey(DocumentLayout Layout, long Revision, DocumentPosition Caret, bool Shade);

    private (ControlRangeKey Key, Dictionary<string, List<ControlRange>> Ranges)? _controls;

    public Dictionary<string, List<ControlRange>> ControlRanges(DocumentLayout layout, bool shade)
    {
        var key = new ControlRangeKey(layout, editor.Document.Revision, editor.Caret, shade);
        if (_controls is { } cached && cached.Key == key) return cached.Ranges;
        var ranges = CollectControlRanges(shade);
        _controls = (key, ranges);
        return ranges;
    }

    private Dictionary<string, List<ControlRange>> CollectControlRanges(bool shade)
    {
        var ranges = new Dictionary<string, List<ControlRange>>(StringComparer.Ordinal);
        var catalog = editor.Services.Controls.Catalog;
        var spans = catalog.All(editor.Document);
        if (spans.Count == 0) return ranges;
        var active = editor.ControlAt(editor.Caret)?.Id;
        foreach (var span in spans)
        {
            var current = span.Id == active;
            if (!shade && !current) continue;
            foreach (var paragraph in services.Order.Between(editor.Document, span.Start, span.End))
            {
                var from = paragraph.Id == span.Start.ParagraphId ? span.Start.Offset : 0;
                var to = paragraph.Id == span.End.ParagraphId ? span.End.Offset : paragraph.Length;
                if (!ranges.TryGetValue(paragraph.Id, out var list)) ranges[paragraph.Id] = list = [];
                list.Add(new ControlRange(from, to, current, paragraph.Id == span.Start.ParagraphId, span.Control.Title));
            }
        }
        return ranges;
    }

    public void PaintControls(DrawingContext context, PageBox page, Dictionary<string, List<ControlRange>> ranges, IBrush? brush)
    {
        if (ranges.Count == 0) return;
        var accent = brush is ISolidColorBrush solid ? solid.Color : Colors.DodgerBlue;
        var shade = new SolidColorBrush(accent, 0.08);
        var frame = new Pen(new SolidColorBrush(accent, 0.75), 0.75);
        var titled = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in page.AllLines)
        {
            if (!ranges.TryGetValue(line.ParagraphId, out var list)) continue;
            var composed = line.Line;
            foreach (var range in list)
            {
                var from = Math.Max(range.From, composed.Start);
                var to = Math.Min(range.To, composed.End);
                if (to < from || to == from && range.To > range.From) continue;
                var left = services.Geometry.XOf(line, from) - 1;
                var right = Math.Max(left + 4, services.Geometry.XOf(line, to) + 1);
                var box = new Rect(left, line.Top, right - left, composed.CaretHeight);
                context.FillRectangle(shade, box);
                if (!range.Active) continue;
                context.DrawRectangle(null, frame, box, 1.5, 1.5);
                if (!range.First || range.Title is not { Length: > 0 } title || !titled.Add(line.ParagraphId)) continue;
                var label = new FormattedText(title, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, 7.5, Brushes.White);
                var tab = new Rect(left, line.Top - label.Height - 2, label.Width + 6, label.Height + 2);
                context.FillRectangle(new SolidColorBrush(accent, 0.9), tab, 1.5f);
                context.DrawText(label, new Point(tab.X + 3, tab.Y + 1));
            }
        }
    }

    public void PaintCommentRanges(DrawingContext context, PageBox page, CommentSheet sheet)
    {
        if (sheet.Ranges.Count == 0) return;
        foreach (var line in page.AllLines)
        {
            if (!sheet.Ranges.TryGetValue(line.ParagraphId, out var ranges)) continue;
            var composed = line.Line;
            foreach (var range in ranges)
            {
                var from = Math.Max(range.From, composed.Start);
                var to = Math.Min(range.To, composed.End);
                if (to <= from) continue;
                var left = services.Geometry.XOf(line, from);
                var right = services.Geometry.XOf(line, to);
                context.FillRectangle(new SolidColorBrush(Color.FromUInt32(range.Color), range.Resolved ? 0.08 : 0.18), new Rect(left, line.Top, Math.Max(1, right - left), composed.CaretHeight));
            }
        }
    }

    public void PaintWaves(DrawingContext context, PageBox page)
    {
        if (!services.Proofing.Enabled) return;
        foreach (var group in page.AllLines.GroupBy(line => line.ParagraphId))
        {
            if (editor.Preedit is not null && group.Key == editor.PreeditAt.ParagraphId) continue;
            if (editor.ContentOf(group.Key) is not { } paragraph) continue;
            foreach (var mark in services.Proofing.Marks(editor.Document, paragraph))
            {
                foreach (var line in group)
                {
                    var from = Math.Max(mark.Start, line.Line.Start);
                    var to = Math.Min(mark.Start + mark.Length, line.Line.End);
                    if (to <= from) continue;
                    var left = services.Geometry.XOf(line, from);
                    var right = services.Geometry.XOf(line, to);
                    using (Turn(context, page, line)) services.Waves.PaintWave(context, new Rect(left, line.Top, right - left, line.Line.CaretHeight), 0.05, mark.Grammar);
                }
            }
        }
    }

    public void PaintCells(DrawingContext context, PageBox page, PulseOffice.Documents.Editing.Tables.TableSelection cells, IBrush? accent)
    {
        var brush = new SolidColorBrush(accent is ISolidColorBrush solid ? solid.Color : Colors.DodgerBlue, 0.22);
        foreach (var table in page.Tables.Where(table => table.TableId == cells.Table.Id))
            foreach (var cell in table.Cells.Where(cell => cells.Range.Contains(cell.Row, cell.Column)))
                context.FillRectangle(brush, new Rect(cell.Bounds.X, cell.Bounds.Y, cell.Bounds.Width, cell.Bounds.Height));
    }

    private double FirstLine(double start, double step, double visibleTop) =>
        visibleTop <= start ? start : start + Math.Floor((visibleTop - start) / step) * step;

    public void PaintGrids(DrawingContext context, PageBox page, double visibleTop, double visibleBottom, double scale, IBrush? accent, PulseOffice.Drawing.Shapes.ObjectBox? helpline)
    {
        var view = services.ViewLayout;
        var content = page.Content;
        var last = Math.Min(content.Bottom, visibleBottom);
        if (view.Grid)
        {
            var dot = new SolidColorBrush(Color.FromArgb(150, 100, 116, 139));
            var size = 1.2 / scale;
            var dots = new StreamGeometry();
            using (var pen = dots.Open())
            {
                for (var y = FirstLine(content.Y, view.GridSpacing, visibleTop); y <= last + 0.01; y += view.GridSpacing)
                    for (var x = content.X; x <= content.Right + 0.01; x += view.GridSpacing)
                    {
                        pen.BeginFigure(new Point(x - size / 2, y - size / 2), true);
                        pen.LineTo(new Point(x + size / 2, y - size / 2));
                        pen.LineTo(new Point(x + size / 2, y + size / 2));
                        pen.LineTo(new Point(x - size / 2, y + size / 2));
                        pen.EndFigure(true);
                    }
            }
            context.DrawGeometry(dot, null, dots);
        }
        if (view.BaselineGrid)
        {
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(90, 37, 99, 235)), 0.5 / scale);
            var spacing = Math.Max(6, (editor.Document.Styles.DefaultCharacter.FontSize ?? 12) * 1.17);
            for (var y = FirstLine(content.Y + spacing, spacing, visibleTop); y <= last; y += spacing)
                context.DrawLine(pen, new Point(content.X, y), new Point(content.Right, y));
        }
        if (view.Helplines && helpline is { } area)
        {
            var pen = new Pen(accent ?? Brushes.DodgerBlue, 0.6 / scale, new DashStyle([4, 3], 0));
            foreach (var x in new[] { area.X, area.X + area.Width })
                context.DrawLine(pen, new Point(x, 0), new Point(x, page.Height));
            foreach (var y in new[] { area.Y, area.Y + area.Height })
                context.DrawLine(pen, new Point(0, y), new Point(page.Width, y));
        }
    }

    private ITurnedFrame? TurnedOwner(PageBox page, LineBox line) =>
        page.TurnedFrames.FirstOrDefault(frame => frame.Lines.Contains(line));

    private DrawingContext.PushedState? Turn(DrawingContext context, PageBox page, LineBox line) =>
        TurnedOwner(page, line) is { } owner
            ? context.PushTransform(new ObjectTransform(owner.RotationDegrees).Around(new Rect(owner.Bounds.X, owner.Bounds.Y, owner.Bounds.Width, owner.Bounds.Height)))
            : null;
}
