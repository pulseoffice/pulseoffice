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
            if (tags.Label is { } label && line.Label is { } labelBox && labelBox.Contains(new Avalonia.Point(x, y))) return label;
            if (y >= line.Body.Top && y <= line.Body.Bottom && x >= line.Body.Left - 1 && x <= line.Body.Right + 1) return tags.Body;
        }
        return null;
    }
}
