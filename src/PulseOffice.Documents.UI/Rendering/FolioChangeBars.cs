using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Documents.Layout.Tree;

namespace PulseOffice.Documents.UI.Rendering;

public interface IFolioChangeBars
{
    Func<LineBox, uint?>? For(TextDocument document);
}

public sealed class FolioChangeBars(IChangeSpans spans) : IFolioChangeBars
{
    public Func<LineBox, uint?>? For(TextDocument document) =>
        !document.Changes.ShowChanges || document.Changes.All.Count == 0 ? null : line => Bar(document, line);

    private uint? Bar(TextDocument document, LineBox line)
    {
        var composed = line.Line;
        foreach (var span in spans.Of(document, line.ParagraphId))
        {
            if (span.Kind == ChangeKind.ParagraphFormat)
            {
                if (composed.IsFirst) return span.Color;
                continue;
            }
            var touches = span.Start == span.End
                ? span.Start >= composed.Start && (span.Start < composed.End || composed.IsLast)
                : span.Start < Math.Max(composed.End, composed.Start + 1) && span.End > composed.Start || span.End == int.MaxValue && composed.IsLast && span.Start >= composed.Start;
            if (touches) return span.Color;
        }
        return null;
    }
}
