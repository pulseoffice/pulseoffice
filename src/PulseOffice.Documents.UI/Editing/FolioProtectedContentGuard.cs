using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Protection;
using PulseOffice.Foundation.Modules;
using PulseOffice.Localization;
using PulseOffice.UI.Commands;
using PulseOffice.UI.Controls.Notifications;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Documents.UI.Editing;

public sealed class FolioProtectedContentGuard(
    IActiveFolioContext context,
    PulseOffice.Shell.IDocumentWorkspace workspace,
    IBlockTree tree,
    IStatusBarService status,
    ITextCatalog texts,
    PulseOffice.Document.Commands.IHistoryReversals reversals,
    IFormFilling forms) : ICommandEffectGuard
{
    private sealed record ParagraphMark(string Id, PulseOffice.Text.Rich.TextParagraph Content, IReadOnlyList<PulseOffice.Documents.Marks.InlineMark> Marks);

    private abstract record GuardedRegion
    {
        public abstract bool Changed(IBlockTree tree, TextDocument document);
    }

    private abstract record ParagraphRegion(IReadOnlyList<ParagraphMark> Paragraphs) : GuardedRegion
    {
        public abstract BlockContainer? Locate(IBlockTree tree, BlockContainer body);

        public virtual bool Released(IBlockTree tree, BlockContainer body) => false;

        public virtual bool Counts(PulseOffice.Documents.Marks.InlineMark mark) => true;

        public override bool Changed(IBlockTree tree, TextDocument document)
        {
            if (Released(tree, document.Body)) return false;
            if (Locate(tree, document.Body) is not { } container) return true;
            var paragraphs = tree.Paragraphs(container).ToList();
            if (paragraphs.Count != Paragraphs.Count) return true;
            for (var index = 0; index < paragraphs.Count; index++)
            {
                var then = Paragraphs[index];
                var now = paragraphs[index];
                if (now.Id != then.Id || !Equals(now.Content, then.Content)) return true;
                if (!ReferenceEquals(now.Marks, then.Marks) && !now.Marks.Where(Counts).SequenceEqual(then.Marks.Where(Counts))) return true;
            }
            return false;
        }
    }

    private sealed record SectionRegion(string Id, IReadOnlyList<ParagraphMark> Paragraphs) : ParagraphRegion(Paragraphs)
    {
        public override BlockContainer? Locate(IBlockTree tree, BlockContainer body) =>
            tree.Blocks(body).OfType<SectionBlock>().FirstOrDefault(section => section.Id == Id)?.Content;

        public override bool Released(IBlockTree tree, BlockContainer body) =>
            tree.Blocks(body).OfType<SectionBlock>().Any(section => section.Id == Id && !section.Protected);
    }

    private sealed record CellRegion(string TableId, BlockContainer Content, IReadOnlyList<ParagraphMark> Paragraphs) : ParagraphRegion(Paragraphs)
    {
        public override BlockContainer? Locate(IBlockTree tree, BlockContainer body) => Cell(tree, body)?.Content;

        public override bool Released(IBlockTree tree, BlockContainer body) => Cell(tree, body) is { Protected: false };

        private TableCell? Cell(IBlockTree tree, BlockContainer body) =>
            tree.Blocks(body).OfType<TableBlock>().Where(table => table.Id == TableId)
                .SelectMany(table => table.Rows).SelectMany(row => row.Cells)
                .FirstOrDefault(cell => ReferenceEquals(cell.Content, Content));
    }

    private sealed record BodyRegion(IReadOnlyList<ParagraphMark> Paragraphs) : ParagraphRegion(Paragraphs)
    {
        public override BlockContainer? Locate(IBlockTree tree, BlockContainer body) => body;

        public override bool Counts(PulseOffice.Documents.Marks.InlineMark mark) => mark is not PulseOffice.Documents.Marks.CommentMark;
    }

    private sealed record FormRegion(IFormFilling Forms, FormOutline Outline) : GuardedRegion
    {
        public override bool Changed(IBlockTree tree, TextDocument document) =>
            Forms.Restricts(document) && !Outline.Matches(Forms.Outline(document));
    }

    public ICommandEffectCheck? Begin(CommandDefinition command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Module is not (ModuleKind.Documents or ModuleKind.Shell)) return null;
        if (workspace.Active?.Module != ModuleKind.Documents || context.Target?.Editor is not { } editor) return null;
        var regions = Guarded(editor.Document);
        return regions.Count == 0 ? null : new Check(this, editor, regions, editor.History.StatePoint, reversals.For(command.Id.Value));
    }

    private List<GuardedRegion> Guarded(TextDocument document)
    {
        var body = document.Body;
        if (document.Protection.Restriction is DocumentRestriction.ReadOnly or DocumentRestriction.Comments)
            return [new BodyRegion(Snapshot(body))];
        var blocks = tree.Blocks(body).ToList();
        List<GuardedRegion> regions =
        [
            .. blocks.OfType<SectionBlock>().Where(section => section.Protected && !section.IsIndex)
                .Select(section => new SectionRegion(section.Id, Snapshot(section.Content))),
            .. blocks.OfType<TableBlock>().SelectMany(table => table.Rows.SelectMany(row => row.Cells)
                .Where(cell => cell.Protected).Select(cell => new CellRegion(table.Id, cell.Content, Snapshot(cell.Content)))),
        ];
        if (forms.Restricts(document)) regions.Add(new FormRegion(forms, forms.Outline(document)));
        return regions;
    }

    private List<ParagraphMark> Snapshot(BlockContainer container) =>
        [.. tree.Paragraphs(container).Select(paragraph => new ParagraphMark(paragraph.Id, paragraph.Content, paragraph.Marks))];

    private bool Changed(FolioEditor editor, IReadOnlyList<GuardedRegion> before) => before.Any(region => region.Changed(tree, editor.Document));

    private void Restore(FolioEditor editor, object? statePoint, PulseOffice.Document.Commands.IHistoryReversal reversal)
    {
        reversal.Revert(editor.History, statePoint);
        status.Report(texts["folio.protect.blocked"], NotificationSeverity.Error);
    }

    private sealed class Check(FolioProtectedContentGuard guard, FolioEditor editor, IReadOnlyList<GuardedRegion> before, object? statePoint, PulseOffice.Document.Commands.IHistoryReversal reversal) : ICommandEffectCheck
    {
        public bool Finish()
        {
            if (!guard.Changed(editor, before)) return false;
            guard.Restore(editor, statePoint, reversal);
            return true;
        }
    }
}
