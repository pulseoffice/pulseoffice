using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;

namespace PulseOffice.Documents.Editing;

public interface IObjectAnchoring
{
    void Move(TextDocument document, EditRecorder recorder, string fromParagraphId, string toParagraphId);

    void MoveMarked(TextDocument document, EditRecorder recorder, IEnumerable<InlineMark> marks, string fromParagraphId, string toParagraphId);
}

public sealed class ObjectAnchoring : IObjectAnchoring
{
    public void Move(TextDocument document, EditRecorder recorder, string fromParagraphId, string toParagraphId) =>
        Retarget(document, recorder, document.Objects.AnchoredTo(fromParagraphId).Select(item => item.Id).ToHashSet(StringComparer.Ordinal), toParagraphId);

    public void MoveMarked(TextDocument document, EditRecorder recorder, IEnumerable<InlineMark> marks, string fromParagraphId, string toParagraphId)
    {
        var ids = marks.OfType<ObjectMark>()
            .Select(mark => document.Objects.Find(mark.ObjectId))
            .OfType<AnchoredObject>()
            .Where(item => item.Anchor.Accept(new AnchorParagraph()) == fromParagraphId)
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);
        Retarget(document, recorder, ids, toParagraphId);
    }

    private void Retarget(TextDocument document, EditRecorder recorder, IReadOnlySet<string> ids, string paragraphId)
    {
        if (ids.Count == 0) return;
        recorder.Touch(document.Objects, () => document.Objects.All, document.Objects.Replace);
        document.Objects.Replace([.. document.Objects.All.Select(item =>
        {
            if (!ids.Contains(item.Id)) return item;
            var copy = item.Clone();
            copy.Anchor = item.Anchor.Accept(new AnchorRetarget(paragraphId));
            return copy;
        })]);
    }
}
