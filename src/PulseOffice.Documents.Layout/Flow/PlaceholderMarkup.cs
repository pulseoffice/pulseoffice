using System.Runtime.CompilerServices;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.Layout.Flow;

public interface IPlaceholderMarkup
{
    TextParagraph Present(ParagraphBlock block, TextParagraph content);
}

public sealed class PlaceholderMarkup(IParagraphText text) : IPlaceholderMarkup
{
    private const uint PlaceholderInk = 0xFF808080;

    private sealed record Presented(IReadOnlyList<InlineMark> Marks, TextParagraph Result);

    private readonly ConditionalWeakTable<TextParagraph, Presented> _cache = new();

    public TextParagraph Present(ParagraphBlock block, TextParagraph content)
    {
        if (!block.Marks.Any(mark => mark is ContentControlMark { Control.ShowingPlaceholder: true })) return content;
        if (_cache.TryGetValue(content, out var cached) && ReferenceEquals(cached.Marks, block.Marks)) return cached.Result;
        var state = new ParagraphState(content, []);
        foreach (var start in block.Marks.OfType<ContentControlMark>().Where(mark => mark is { Edge: MarkEdge.Start, Control.ShowingPlaceholder: true }))
        {
            var end = block.Marks.OfType<ContentControlMark>().FirstOrDefault(mark => mark.Edge == MarkEdge.End && mark.ControlId == start.ControlId)?.Offset ?? content.Text.Length;
            var from = Math.Clamp(start.Offset, 0, content.Text.Length);
            var to = Math.Clamp(end, from, content.Text.Length);
            if (to > from) state = text.Map(state, from, to, run => run with { Color = PlaceholderInk });
        }
        _cache.AddOrUpdate(content, new Presented(block.Marks, state.Content));
        return state.Content;
    }
}
