using System.Globalization;
using Avalonia;
using Avalonia.Media;
using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Commenting;
using PulseOffice.Documents.Layout.Queries;
using PulseOffice.Documents.Layout.Tree;
using PulseOffice.Documents.Revisions;
using PulseOffice.Documents.UI.Rendering;

namespace PulseOffice.Documents.UI.Canvas;

public sealed record CommentBalloon(CommentAnchor Anchor, Rect Bounds, Point Origin, Color Ink, FormattedText Header, FormattedText Body);

public sealed record CommentRange(int From, int To, uint Color, bool Resolved);

public sealed record CommentSheet(IReadOnlyList<CommentAnchor> Anchors, IReadOnlyDictionary<string, uint> Colors)
{
    public IReadOnlyDictionary<string, IReadOnlyList<CommentRange>> Ranges { get; init; } = new Dictionary<string, IReadOnlyList<CommentRange>>();
}

public interface IFolioCommentMargin
{
    double Width { get; }

    bool Shows(TextDocument document);

    CommentSheet Sheet(TextDocument document);

    IReadOnlyList<CommentBalloon> Place(TextDocument document, CommentSheet sheet, PageBox page);

    void Paint(DrawingContext context, PageBox page, IReadOnlyList<CommentBalloon> balloons, string? current, double scale);
}

public sealed class FolioCommentMargin(ICommentAnchors anchors, IAuthorColors colors, IFolioViewMarks marks, ILineOffsets geometry, PulseOffice.Documents.UI.Editing.IParagraphOrder order) : IFolioCommentMargin
{
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<PageBox, Tuple<CommentSheet, IReadOnlyList<CommentBalloon>>> _placed = new();

    private const double Gutter = 10;
    private const double Padding = 6;
    private const double Spacing = 6;
    private const int BodyLines = 6;

    private readonly Typeface _typeface = new(FontFamily.Default);
    private readonly Typeface _bold = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    public double Width => 190;

    public bool Shows(TextDocument document) => marks.Comments && document.Comments.All.Any(comment => marks.ResolvedComments || !comment.Resolved);

    private (TextDocument? Document, long Revision, bool Resolved, CommentSheet? Sheet) _cached;

    public CommentSheet Sheet(TextDocument document)
    {
        if (ReferenceEquals(_cached.Document, document) && _cached.Revision == document.Revision && _cached.Resolved == marks.ResolvedComments && _cached.Sheet is { } sheet) return sheet;
        var shown = anchors.All(document).Where(anchor => marks.ResolvedComments || !anchor.Comment.Resolved).ToList();
        var palette = colors.Of(document);
        var built = new CommentSheet(shown, palette) { Ranges = Ranges(document, shown, palette) };
        _cached = (document, document.Revision, marks.ResolvedComments, built);
        return built;
    }

    private Dictionary<string, IReadOnlyList<CommentRange>> Ranges(TextDocument document, IReadOnlyList<CommentAnchor> shown, IReadOnlyDictionary<string, uint> palette)
    {
        var ranges = new Dictionary<string, List<CommentRange>>(StringComparer.Ordinal);
        foreach (var anchor in shown.Where(anchor => anchor.Start != anchor.End))
        {
            var color = palette.TryGetValue(anchor.Comment.Author, out var found) ? found : 0xFF808080;
            if (anchor.Start.ParagraphId == anchor.End.ParagraphId)
            {
                Add(ranges, anchor.Start.ParagraphId, new CommentRange(anchor.Start.Offset, anchor.End.Offset, color, anchor.Comment.Resolved));
                continue;
            }
            if (!order.Contains(document, anchor.Start.ParagraphId) || !order.Contains(document, anchor.End.ParagraphId)) continue;
            foreach (var paragraph in order.Between(document, anchor.Start, anchor.End))
            {
                var from = paragraph.Id == anchor.Start.ParagraphId ? anchor.Start.Offset : 0;
                var to = paragraph.Id == anchor.End.ParagraphId ? anchor.End.Offset : paragraph.Length;
                Add(ranges, paragraph.Id, new CommentRange(from, to, color, anchor.Comment.Resolved));
            }
        }
        return ranges.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<CommentRange>)pair.Value, StringComparer.Ordinal);
    }

    private void Add(Dictionary<string, List<CommentRange>> ranges, string paragraphId, CommentRange range)
    {
        if (!ranges.TryGetValue(paragraphId, out var list)) ranges[paragraphId] = list = [];
        list.Add(range);
    }

    public IReadOnlyList<CommentBalloon> Place(TextDocument document, CommentSheet sheet, PageBox page)
    {
        if (_placed.TryGetValue(page, out var cached) && ReferenceEquals(cached.Item1, sheet)) return cached.Item2;
        var balloons = Compute(document, sheet, page);
        _placed.AddOrUpdate(page, Tuple.Create(sheet, balloons));
        return balloons;
    }

    private IReadOnlyList<CommentBalloon> Compute(TextDocument document, CommentSheet sheet, PageBox page)
    {
        var lines = page.AllLines.ToLookup(line => line.ParagraphId, StringComparer.Ordinal);
        var placed = new List<(CommentAnchor Anchor, Point Origin)>();
        foreach (var anchor in sheet.Anchors)
        {
            if (!lines.Contains(anchor.Start.ParagraphId)) continue;
            var line = lines[anchor.Start.ParagraphId].FirstOrDefault(candidate =>
                anchor.Start.Offset >= candidate.Line.Start && (anchor.Start.Offset < candidate.Line.End || candidate.Line.IsLast));
            if (line is null) continue;
            placed.Add((anchor, new Point(geometry.XOf(line, anchor.Start.Offset), line.Top + line.Line.Height)));
        }
        var result = new List<CommentBalloon>();
        var bottom = 0.0;
        var width = Width - Gutter * 2;
        foreach (var (anchor, origin) in placed.OrderBy(item => item.Origin.Y).ThenBy(item => item.Origin.X))
        {
            var ink = Color.FromUInt32(sheet.Colors.TryGetValue(anchor.Comment.Author, out var found) ? found : 0xFF808080);
            var header = Text(Header(anchor.Comment, document), _bold, 7.5, ink, width - Padding * 2, 1);
            var body = Text(Body(document, anchor.Comment), _typeface, 8, Color.FromRgb(0x22, 0x22, 0x22), width - Padding * 2, BodyLines);
            var height = header.Height + body.Height + Padding * 2 + 2;
            var top = Math.Max(origin.Y - 12, bottom + Spacing);
            var bounds = new Rect(page.Width + Gutter, top, width, height);
            result.Add(new CommentBalloon(anchor, bounds, origin, ink, header, body));
            bottom = bounds.Bottom;
        }
        return result;
    }

    public void Paint(DrawingContext context, PageBox page, IReadOnlyList<CommentBalloon> balloons, string? current, double scale)
    {
        foreach (var balloon in balloons)
        {
            var chosen = balloon.Anchor.Comment.Id == current;
            var faded = balloon.Anchor.Comment.Resolved && !chosen;
            var ink = balloon.Ink;
            var stroke = new SolidColorBrush(ink, faded ? 0.5 : 1);
            var pen = new Pen(stroke, (chosen ? 1.6 : 0.8) / Math.Max(0.1, scale)) { DashStyle = chosen ? null : DashStyle.Dash };
            var elbow = new Point(page.Width, balloon.Origin.Y);
            context.DrawLine(pen, balloon.Origin, elbow);
            context.DrawLine(pen, elbow, new Point(balloon.Bounds.X, balloon.Bounds.Y + 8));
            var fill = new SolidColorBrush(faded ? Color.FromRgb(0xF4, 0xF4, 0xF4) : Color.FromArgb(255, Blend(ink.R), Blend(ink.G), Blend(ink.B)));
            context.DrawRectangle(fill, new Pen(stroke, (chosen ? 1.6 : 0.8) / Math.Max(0.1, scale)), new RoundedRect(balloon.Bounds, 4));
            context.DrawRectangle(stroke, null, new Rect(balloon.Bounds.X, balloon.Bounds.Y, 3, balloon.Bounds.Height));
            using var opacity = context.PushOpacity(faded ? 0.6 : 1);
            context.DrawText(balloon.Header, new Point(balloon.Bounds.X + Padding, balloon.Bounds.Y + Padding));
            context.DrawText(balloon.Body, new Point(balloon.Bounds.X + Padding, balloon.Bounds.Y + Padding + balloon.Header.Height + 2));
        }
    }

    private byte Blend(byte channel) => (byte)(channel + (255 - channel) * 0.88);

    private FormattedText Text(string text, Typeface typeface, double size, Color color, double width, int lines) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, size, new SolidColorBrush(color))
        {
            MaxTextWidth = Math.Max(20, width),
            MaxLineCount = lines,
            Trimming = TextTrimming.CharacterEllipsis,
        };

    private string Header(Comment comment, TextDocument document)
    {
        var date = comment.Date == default ? string.Empty : "  " + comment.Date.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
        var replies = document.Comments.Thread(comment.Id).Count() - 1;
        return (comment.Resolved ? "✓ " : string.Empty) + comment.Author + date + (replies > 0 ? "  ↳" + replies.ToString(CultureInfo.CurrentCulture) : string.Empty);
    }

    private string Body(TextDocument document, Comment root)
    {
        var parts = new List<string> { Plain(root) };
        parts.AddRange(document.Comments.Thread(root.Id).Skip(1).Select(reply => "↳ " + reply.Author + ": " + Plain(reply)));
        return string.Join("\n", parts);
    }

    private string Plain(Comment comment) => string.Join(" ", comment.Content.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
}
