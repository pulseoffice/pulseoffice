using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Text.Rich;

namespace PulseOffice.Documents.UI.Shell.Snapshot;

public interface IFolioSnapshotValidator
{
    bool IsSound(TextDocument document);
}

public sealed class FolioSnapshotValidator : IFolioSnapshotValidator
{
    public bool IsSound(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Body?.Blocks is null || !document.Body.Blocks.Any(block => block is ParagraphBlock)) return false;
        if (!AllPresent(document.Comments.All) || !AllPresent(document.Notes.All) || !AllPresent(document.Objects.All) || !AllPresent(document.Changes.All)) return false;
        return Containers(document).All(Sound);
    }

    private IEnumerable<BlockContainer?> Containers(TextDocument document) =>
        new BlockContainer?[] { document.Body }
            .Concat(document.Comments.All.Select(comment => comment.Content))
            .Concat(document.Notes.All.Select(note => note.Content))
            .Concat(document.Objects.All.Select(item => item.Content.Story).OfType<BlockContainer>())
            .Concat(document.Styles.Page.All.SelectMany(page => new[] { page.Header, page.Footer }).Where(part => part is not null)
                .SelectMany(part => new[] { part.Default, part.First, part.Even }).Where(container => container is not null));

    private bool AllPresent<T>(IEnumerable<T?> items) where T : class => items.All(item => item is not null);

    private bool Sound(BlockContainer? container) =>
        container?.Blocks is { } blocks && blocks.All(block => block is not null && block.Id is not null && block.Accept(new BlockSoundness(this)));

    private bool Sound(TextParagraph? paragraph) =>
        paragraph is { Text: not null, Runs: { } runs } && runs.All(run => run is { Text: not null });

    private sealed class BlockSoundness(FolioSnapshotValidator owner) : IBlockVisitor<bool>
    {
        public bool VisitParagraph(ParagraphBlock paragraph) => owner.Sound(paragraph.Content) && paragraph.Marks is not null && paragraph.Marks.All(mark => mark is not null);

        public bool VisitTable(TableBlock table) =>
            table.Rows.All(row => row is { Cells: { } cells } && cells.All(cell => cell is not null && cell.ColumnSpan >= 1 && cell.RowSpan >= 1 && owner.Sound(cell.Content)));

        public bool VisitSection(SectionBlock section) => owner.Sound(section.Content);
    }
}
