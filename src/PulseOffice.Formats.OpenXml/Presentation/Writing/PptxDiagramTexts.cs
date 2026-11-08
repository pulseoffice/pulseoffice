using PulseOffice.Presentations;

namespace PulseOffice.Formats.OpenXml;

internal sealed class PptxDiagramTexts
{
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Edits(Slide slide, NativeDiagram diagram)
    {
        ArgumentNullException.ThrowIfNull(slide);
        ArgumentNullException.ThrowIfNull(diagram);
        var edits = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var member in diagram.Members)
        {
            if (slide.Current(member) is not { } current || ReferenceEquals(current, member)) continue;
            if (!diagram.PointsByMember.TryGetValue(member.Id, out var point)) continue;
            var before = Paragraphs(member);
            var after = Paragraphs(current);
            if (after is not null && (before is null || !before.SequenceEqual(after, StringComparer.Ordinal))) edits[point] = after;
        }
        return edits;
    }

    private IReadOnlyList<string>? Paragraphs(SlideElement element) => element switch
    {
        TextElement text => [.. text.Text.Paragraphs.Select(paragraph => paragraph.Text)],
        ObjectElement { Content: PulseOffice.Drawing.Shapes.ShapeObject { Text: { } text } } => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'),
        _ => null,
    };
}
