using PulseOffice.Documents.Annotations;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Objects;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Wordprocessing;

public sealed class DocxParagraphMarkJoins(DocxParagraphMarkChanges paragraphMarks, IParagraphText text)
{
    private sealed record Signature(string Author, DateTimeOffset Date);

    public bool MarkDeleted(W.Paragraph element) => paragraphMarks.Deletion(element) is not null;

    public bool Joins(W.Paragraph element) => MarkDeleted(element) && element.ParagraphProperties?.SectionProperties is null;

    public ParagraphBlock Join(IReadOnlyList<(ParagraphBlock Block, W.Paragraph Element)> chain, ParagraphBlock next, DocxReadContext context)
    {
        var paragraphs = chain.Select(item => item.Block).Append(next).ToList();
        var signature = SignatureOf(chain[0].Element);
        var (first, tail) = Trailing(paragraphs[0].State, signature, context);
        var parts = new List<ParagraphState> { first };
        var pieces = new List<ParagraphBlock> { Piece(paragraphs[0], tail, context) };
        for (var index = 1; index < paragraphs.Count; index++)
        {
            var current = paragraphs[index];
            Retarget(current.Id, next.Id, context);
            var (state, head) = Leading(current.State, signature, context);
            pieces.Add(Piece(current, head, context));
            var last = index == paragraphs.Count - 1;
            if (state.Length == 0 && !last)
            {
                parts.Add(state);
                continue;
            }
            var deletion = new Deletion(context.Document.NextId("change"), signature.Author, signature.Date, new BlockContainer(pieces));
            context.Document.Changes.Add(deletion);
            var joint = new ChangeMark(0, deletion.Id, MarkEdge.Point);
            if (last)
            {
                parts.Add(state with { Marks = [joint, .. state.Marks] });
                break;
            }
            signature = SignatureOf(chain[index].Element);
            var (kept, trailing) = Trailing(state, signature, context);
            parts.Add(kept with { Marks = [joint, .. kept.Marks] });
            pieces = [Piece(current, trailing, context)];
        }
        Retarget(paragraphs[0].Id, next.Id, context);
        var merged = Concat(parts, 0, parts.Count);
        return new ParagraphBlock(next.Id, merged.Content) { Marks = merged.Marks };
    }

    private ParagraphState Concat(IReadOnlyList<ParagraphState> parts, int start, int end)
    {
        if (end - start == 1) return parts[start];
        var middle = start + (end - start) / 2;
        return text.Concat(Concat(parts, start, middle), Concat(parts, middle, end));
    }

    private Signature SignatureOf(W.Paragraph element)
    {
        var mark = paragraphMarks.Deletion(element)!;
        var date = mark.Date?.Value is { } when ? new DateTimeOffset(DateTime.SpecifyKind(when, DateTimeKind.Utc)) : DateTimeOffset.UnixEpoch;
        return new Signature(mark.Author?.Value ?? string.Empty, date);
    }

    private (ParagraphState State, ParagraphBlock? Removed) Trailing(ParagraphState state, Signature signature, DocxReadContext context) =>
        Absorb(state, state.Marks.LastOrDefault(mark => mark.Offset == state.Length && Absorbable(mark, signature, context)), context);

    private (ParagraphState State, ParagraphBlock? Removed) Leading(ParagraphState state, Signature signature, DocxReadContext context) =>
        Absorb(state, state.Marks.FirstOrDefault(mark => mark.Offset == 0 && Absorbable(mark, signature, context)), context);

    private bool Absorbable(InlineMark mark, Signature signature, DocxReadContext context) =>
        mark is ChangeMark { Edge: MarkEdge.Point } change
        && context.Document.Changes.Find(change.ChangeId) is Deletion { WithinInsertion: null, Removed.Blocks: [ParagraphBlock] } deletion
        && deletion.Author == signature.Author;

    private (ParagraphState State, ParagraphBlock? Removed) Absorb(ParagraphState state, InlineMark? mark, DocxReadContext context)
    {
        if (mark is not ChangeMark change || context.Document.Changes.Find(change.ChangeId) is not Deletion deletion) return (state, null);
        context.Document.Changes.Remove(deletion.Id);
        return (state with { Marks = [.. state.Marks.Where(item => !ReferenceEquals(item, mark))] }, (ParagraphBlock)deletion.Removed.Blocks[0]);
    }

    private ParagraphBlock Piece(ParagraphBlock paragraph, ParagraphBlock? removed, DocxReadContext context) =>
        new(removed?.Id ?? context.Document.NextId("p"), paragraph.Content.WithRuns(removed?.Content.Runs ?? [])) { Marks = removed?.Marks ?? [] };

    private void Retarget(string from, string to, DocxReadContext context)
    {
        if (from == to) return;
        foreach (var item in context.Document.Objects.AnchoredTo(from).ToList())
            item.Anchor = item.Anchor.Accept(new AnchorRetarget(to));
        Retarget(context.CarrierStarts, from, to);
        Retarget(context.CarrierEnds, from, to);
    }

    private void Retarget(Dictionary<string, string> carriers, string from, string to)
    {
        foreach (var carrier in carriers.Where(entry => entry.Value == from).Select(entry => entry.Key).ToList()) carriers[carrier] = to;
    }
}
