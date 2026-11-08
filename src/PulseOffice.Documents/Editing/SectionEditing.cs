using PulseOffice.Document.Commands;
using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Editing;

public sealed record SectionSettings(string Name, SectionColumns Columns, bool Protected, bool Hidden)
{
    public byte[]? ProtectionKey { get; init; }
}

public interface ISectionEditing
{
    IReadOnlyList<SectionBlock> Sections(TextDocument document);

    DocumentEdit Wrap(TextDocument document, UndoStack history, DocumentRange range, SectionSettings settings);

    DocumentEdit Change(TextDocument document, UndoStack history, string sectionId, SectionSettings settings);

    DocumentEdit Unwrap(TextDocument document, UndoStack history, string sectionId);

    SectionBlock? Around(TextDocument document, string paragraphId);
}

public sealed class SectionEditing(IStoryCatalog stories, IBlockTree tree) : ISectionEditing
{
    public IReadOnlyList<SectionBlock> Sections(TextDocument document) =>
        [.. stories.Stories(document).SelectMany(Walk)];

    public SectionBlock? Around(TextDocument document, string paragraphId) =>
        Sections(document).LastOrDefault(section => tree.Paragraphs(section.Content).Any(paragraph => paragraph.Id == paragraphId));

    public DocumentEdit Wrap(TextDocument document, UndoStack history, DocumentRange range, SectionSettings settings)
    {
        var first = stories.Find(document, range.Start.ParagraphId);
        var container = first.Location.Container;
        var start = first.Location.Index;
        var end = range.End.ParagraphId == range.Start.ParagraphId ? start : container.IndexOf(range.End.ParagraphId);
        if (end < start) end = start;
        var recorder = new EditRecorder(document, "section-insert").Touch(container);
        var blocks = container.Blocks.Skip(start).Take(end - start + 1).ToList();
        container.RemoveRange(start, blocks.Count);
        var section = Apply(new SectionBlock(document.NextId("s"), settings.Name, new BlockContainer(blocks)), settings);
        container.Insert(start, section);
        return recorder.Commit(history);
    }

    public DocumentEdit Change(TextDocument document, UndoStack history, string sectionId, SectionSettings settings)
    {
        var recorder = new EditRecorder(document, "section-change");
        if (Locate(document, sectionId) is not { } location) return recorder.Commit(history);
        recorder.Touch(location.Container);
        var section = (SectionBlock)location.Container[location.Index];
        location.Container.RemoveAt(location.Index);
        location.Container.Insert(location.Index, Apply(new SectionBlock(section.Id, settings.Name, section.Content)
        {
            Condition = section.Condition,
            LinkedFile = section.LinkedFile,
            Index = section.Index,
        }, settings));
        return recorder.Commit(history);
    }

    public DocumentEdit Unwrap(TextDocument document, UndoStack history, string sectionId)
    {
        var recorder = new EditRecorder(document, "section-remove");
        if (Locate(document, sectionId) is not { } location) return recorder.Commit(history);
        recorder.Touch(location.Container);
        var section = (SectionBlock)location.Container[location.Index];
        location.Container.RemoveAt(location.Index);
        location.Container.InsertRange(location.Index, section.Content.Blocks);
        return recorder.Commit(history);
    }

    private SectionBlock Apply(SectionBlock section, SectionSettings settings)
    {
        section.Columns = settings.Columns;
        section.Protected = settings.Protected;
        section.ProtectionKey = settings.Protected ? settings.ProtectionKey : null;
        section.Hidden = settings.Hidden;
        return section;
    }

    private (BlockContainer Container, int Index)? Locate(TextDocument document, string sectionId)
    {
        foreach (var story in stories.Stories(document))
            if (tree.Locate(story, sectionId) is { Block: SectionBlock } location) return (location.Container, location.Index);
        return null;
    }

    private IEnumerable<SectionBlock> Walk(BlockContainer container)
    {
        foreach (var block in container.Blocks)
        {
            if (block is SectionBlock section)
            {
                yield return section;
                foreach (var inner in Walk(section.Content)) yield return inner;
            }
            else if (block is TableBlock table)
            {
                foreach (var inner in table.Rows.SelectMany(row => row.Cells).SelectMany(cell => Walk(cell.Content))) yield return inner;
            }
        }
    }
}
