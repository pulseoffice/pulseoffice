using PulseOffice.Document.Commands;

namespace PulseOffice.Documents.Editing;

public sealed record TypingRun(string ParagraphId, int Start, int End, char First, char Last) : IUndoMergeKey
{
    public static TypingRun? Of(DocumentPosition at, string text) =>
        text.Length == 0 || text.Contains('\n') || text.Contains('\r') ? null
            : new TypingRun(at.ParagraphId, at.Offset, at.Offset + text.Length, text[0], text[^1]);

    public bool Continues(IUndoMergeKey previous) =>
        previous is TypingRun earlier
        && earlier.ParagraphId == ParagraphId
        && Start >= earlier.Start
        && Start <= earlier.End
        && !(char.IsWhiteSpace(earlier.Last) && !char.IsWhiteSpace(First));

    public IUndoMergeKey Join(IUndoMergeKey previous) =>
        previous is TypingRun earlier ? this with { Start = earlier.Start, First = earlier.First } : this;
}
