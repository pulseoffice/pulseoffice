using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Editing;
using PulseOffice.Documents.Indexes;

namespace PulseOffice.Documents.Protection;

public enum EditVerdict
{
    Allowed,
    Generated,
    ProtectedSection,
    ProtectedDocument,
    ProtectedCell,
}

public interface IEditGuard
{
    EditVerdict Check(TextDocument document, string paragraphId);

    EditVerdict CheckContent(TextDocument document, string paragraphId);

    bool CanComment(TextDocument document);

    bool CanResolveChanges(TextDocument document);

    bool ForcesRecording(TextDocument document);

    bool Protects(TextDocument document, IReadOnlyList<ParagraphBlock> paragraphs, DocumentRange range);
}

public sealed class EditGuard(IGeneratedRegions generated, IContentTrails trails) : IEditGuard
{
    public EditVerdict Check(TextDocument document, string paragraphId) => Check(document, paragraphId, true);

    public EditVerdict CheckContent(TextDocument document, string paragraphId) => Check(document, paragraphId, false);

    private EditVerdict Check(TextDocument document, string paragraphId, bool restricted)
    {
        ContentTrail trail;
        try
        {
            trail = trails.Find(document, paragraphId);
        }
        catch (KeyNotFoundException)
        {
            return EditVerdict.Allowed;
        }
        var inComment = document.Comments.All.Any(comment => ReferenceEquals(comment.Content, trail.Story));
        if (restricted && !inComment && document.Protection.Restriction is DocumentRestriction.ReadOnly or DocumentRestriction.Comments or DocumentRestriction.Forms)
            return EditVerdict.ProtectedDocument;
        if (restricted && inComment && document.Protection.Restriction == DocumentRestriction.ReadOnly) return EditVerdict.ProtectedDocument;
        if (generated.Contains(document, paragraphId)) return EditVerdict.Generated;
        if (trail.Steps.Any(step => step.Block is SectionBlock { Protected: true, IsIndex: false })) return EditVerdict.ProtectedSection;
        return InProtectedCell(trail) ? EditVerdict.ProtectedCell : EditVerdict.Allowed;
    }

    private bool InProtectedCell(ContentTrail trail)
    {
        for (var index = 0; index + 1 < trail.Steps.Count; index++)
        {
            if (trail.Steps[index].Block is not TableBlock table) continue;
            var owner = trail.Steps[index + 1].Container;
            if (table.Rows.Any(row => row.Cells.Any(cell => cell.Protected && ReferenceEquals(cell.Content, owner)))) return true;
        }
        return false;
    }

    public bool Protects(TextDocument document, IReadOnlyList<ParagraphBlock> paragraphs, DocumentRange range)
    {
        if (document.Marks is { Fields: false, Bookmarks: false } || range.IsCollapsed) return false;
        foreach (var paragraph in paragraphs)
        {
            var from = paragraph.Id == range.Start.ParagraphId ? range.Start.Offset : 0;
            var to = paragraph.Id == range.End.ParagraphId ? range.End.Offset : paragraph.Length + 1;
            if (document.Marks.Fields && Fields(paragraph).Any(field => field.Start < to && field.End > from)) return true;
            if (document.Marks.Bookmarks && paragraph.Marks.OfType<Marks.BookmarkMark>().Any(mark => mark.Offset >= from && mark.Offset < to && !(mark.Offset == from && paragraph.Id == range.Start.ParagraphId && mark.Edge == Marks.MarkEdge.End))) return true;
        }
        return false;
    }

    private IEnumerable<(int Start, int End)> Fields(ParagraphBlock paragraph)
    {
        var offset = 0;
        foreach (var run in paragraph.Content.Runs)
        {
            if (run.Field != PulseOffice.Text.Rich.TextFieldKind.None) yield return (offset, offset + run.Text.Length);
            offset += run.Text.Length;
        }
    }

    public bool CanComment(TextDocument document) => document.Protection.Restriction is DocumentRestriction.None or DocumentRestriction.Comments or DocumentRestriction.TrackedChanges;

    public bool CanResolveChanges(TextDocument document) =>
        document.Changes.ProtectionKey is null && document.Protection.Restriction is DocumentRestriction.None;

    public bool ForcesRecording(TextDocument document) =>
        document.Changes.ProtectionKey is not null || document.Protection.Restriction == DocumentRestriction.TrackedChanges;
}
