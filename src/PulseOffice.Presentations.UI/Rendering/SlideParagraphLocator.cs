using PulseOffice.Printing;

namespace PulseOffice.Presentations.UI.Rendering;

public sealed record SlideParagraphTags(PrintTag Body, PrintTag? Label);

public sealed class SlideParagraphLocator(SlideElementPlacement placement, IReadOnlyDictionary<int, SlideParagraphTags> paragraphs) : IPrintSegmentLocator
{
    public PrintTag? At(double xPoints, double yPoints)
    {
        var (x, y) = placement.Unrotated(xPoints, yPoints);
        foreach (var line in placement.Lines)
        {
            if (!paragraphs.TryGetValue(line.Paragraph, out var tags)) continue;
            var local = line.Local(new Avalonia.Point(x, y));
            if (tags.Label is { } label && line.Label is { } labelBox && labelBox.Contains(local)) return label;
            if (local.Y >= line.Body.Top && local.Y <= line.Body.Bottom && local.X >= line.Body.Left - 1 && local.X <= line.Body.Right + 1) return tags.Body;
        }
        return null;
    }
}
