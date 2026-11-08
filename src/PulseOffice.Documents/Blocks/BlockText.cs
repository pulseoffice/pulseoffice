namespace PulseOffice.Documents.Blocks;

public sealed class BlockText : IBlockVisitor<string>
{
    public string Of(BlockContainer container) => string.Join("\n", container.Blocks.Select(block => block.Accept(this)));

    public string VisitParagraph(ParagraphBlock paragraph) => paragraph.Text;

    public string VisitSection(SectionBlock section) => Of(section.Content);

    public string VisitTable(TableBlock table) =>
        string.Join("\n", table.Rows.Select(row => string.Join("\t", row.Cells.Where(cell => !cell.Covered).Select(cell => Of(cell.Content)))));
}
