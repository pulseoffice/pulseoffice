using Avalonia;
using PulseOffice.Text.Rich;

namespace PulseOffice.UI.Controls.Text.Rich;

public sealed record RichTextLink(string Address, string? Tip);

public sealed record RichTextLinkArea(Rect Bounds, RichTextLink Link);

public interface IRichTextLinkHit
{
    RichTextLink? At(ComposedTextFrame composed, TextFields fields, Point point);

    IReadOnlyList<RichTextLinkArea> Areas(ComposedTextFrame composed, TextFields fields);
}

public sealed class RichTextLinkHit(RunText runText) : IRichTextLinkHit
{
    public RichTextLinkHit() : this(new RunText())
    {
    }

    public RichTextLink? At(ComposedTextFrame composed, TextFields fields, Point point)
    {
        ArgumentNullException.ThrowIfNull(composed);
        if (!composed.Placement.TryInvert(out var inverse)) return null;
        var local = point.Transform(inverse);
        for (var index = 0; index < composed.Lines.Count; index++)
        {
            var line = composed.Lines[index];
            foreach (var segment in line.Segments)
            {
                var origin = composed.Origin(index, segment);
                if (!new Rect(origin, new Size(segment.Text.WidthIncludingTrailingWhitespace, segment.Text.Height)).Contains(local)) continue;
                if (RunAt(line, segment, origin, fields, local) is { } link) return link;
            }
        }
        return null;
    }

    public IReadOnlyList<RichTextLinkArea> Areas(ComposedTextFrame composed, TextFields fields)
    {
        ArgumentNullException.ThrowIfNull(composed);
        var areas = new List<RichTextLinkArea>();
        for (var index = 0; index < composed.Lines.Count; index++)
        {
            var line = composed.Lines[index];
            foreach (var segment in line.Segments)
            {
                var origin = composed.Origin(index, segment);
                var position = line.Offset;
                foreach (var run in line.Paragraph.EffectiveRuns())
                {
                    var length = runText.Shown(run, fields).Length;
                    var from = Math.Max(position, segment.Start);
                    var to = Math.Min(position + length, segment.End);
                    position += length;
                    if (run.Hyperlink is not { Length: > 0 } address || to <= from) continue;
                    if (segment.Text.BuildHighlightGeometry(origin, from - segment.Start, to - from) is not { } geometry) continue;
                    var bounds = geometry.Bounds.TransformToAABB(composed.Placement);
                    if (bounds.Width > 0 && bounds.Height > 0) areas.Add(new RichTextLinkArea(bounds, new RichTextLink(address, run.HyperlinkTip)));
                }
            }
        }
        return areas;
    }

    private RichTextLink? RunAt(TypesetLine line, TypesetSegment segment, Point origin, TextFields fields, Point local)
    {
        var position = line.Offset;
        foreach (var run in line.Paragraph.EffectiveRuns())
        {
            var length = runText.Shown(run, fields).Length;
            var from = Math.Max(position, segment.Start);
            var to = Math.Min(position + length, segment.End);
            position += length;
            if (run.Hyperlink is not { Length: > 0 } address || to <= from) continue;
            if (segment.Text.BuildHighlightGeometry(origin, from - segment.Start, to - from) is { } area && area.FillContains(local))
                return new RichTextLink(address, run.HyperlinkTip);
        }
        return null;
    }
}
