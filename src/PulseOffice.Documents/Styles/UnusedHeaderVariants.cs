using PulseOffice.Documents.Blocks;

namespace PulseOffice.Documents.Styles;

public interface IUnusedHeaderVariants
{
    bool EvenPagesShownInDocument(TextDocument document);

    bool HiddenEvenShownByDocumentSetting(TextDocument document);
}

public sealed class UnusedHeaderVariants(IBlockTree tree) : IUnusedHeaderVariants
{
    public bool EvenPagesShownInDocument(TextDocument document) =>
        document.Styles.Page.All.Any(page => page.EvenPagesDifferent);

    public bool HiddenEvenShownByDocumentSetting(TextDocument document) =>
        EvenPagesShownInDocument(document)
        && document.Styles.Page.All.Where(page => !page.EvenPagesDifferent).Any(page => new[] { page.Header, page.Footer }.Any(part => Filled(document, part.Even)));

    private bool Filled(TextDocument document, BlockContainer? content) =>
        content is not null && (content.Blocks.Any(block => block is not ParagraphBlock) || tree.Paragraphs(content).Any(paragraph => paragraph.Text.Length > 0 || paragraph.Content.Runs.Any(run => run.Field != PulseOffice.Text.Rich.TextFieldKind.None) || document.Objects.AnchoredTo(paragraph.Id).Any()));
}
