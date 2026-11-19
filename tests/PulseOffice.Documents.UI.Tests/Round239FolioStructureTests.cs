using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round239FolioStructureTests
{
    private sealed class FixedOutline(string headingId) : IDocumentOutline
    {
        public int? LevelOf(TextDocument document, ParagraphBlock paragraph) => paragraph.Id == headingId ? 2 : null;

        public IReadOnlyList<OutlineEntry> Headings(TextDocument document, int maximumLevel = 10) =>
            [.. document.Body.Blocks.OfType<ParagraphBlock>().Where(paragraph => paragraph.Id == headingId).Select(paragraph => new OutlineEntry(paragraph, 2))];
    }

    private static ParagraphBlock Item(string id, string list) =>
        new(id, new TextParagraph(id) { ListStyleName = "Bullets", ListId = list, Runs = [new TextRun(id)] });

    [Fact]
    public void Headings_lists_and_table_cells_get_their_structure_roles()
    {
        var document = new TextDocument();
        document.Body.Add(new ParagraphBlock("h", "Titolo"));
        document.Body.Add(Item("a", "L1"));
        document.Body.Add(Item("b", "L1"));
        document.Body.Add(new ParagraphBlock("p", "Testo"));
        document.Body.Add(Item("c", "L1"));
        var table = new TableBlock("t", [
            new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("th", "Nome")]))]),
            new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("td", "Anna")]))]),
        ], [Twip.FromCentimeters(5)]) { HeaderRows = 1 };
        document.Body.Add(table);

        var structure = new FolioStructureIndex(new FixedOutline("h")).Index(document);

        Assert.Equal(PrintRole.Heading, structure.Paragraphs["h"].Role);
        Assert.Equal(2, structure.Paragraphs["h"].Level);
        Assert.Equal(PrintRole.Paragraph, structure.Paragraphs["p"].Role);
        Assert.Equal(PrintRole.ListBody, structure.Paragraphs["a"].Role);
        Assert.Equal(PrintRole.ListLabel, structure.Labels["a"].Role);
        Assert.Equal(structure.Paragraphs["a"].Parent?.Parent?.Key, structure.Paragraphs["b"].Parent?.Parent?.Key);
        Assert.NotEqual(structure.Paragraphs["a"].Parent?.Parent?.Key, structure.Paragraphs["c"].Parent?.Parent?.Key);
        Assert.Equal(PrintRole.TableHeader, structure.Paragraphs["th"].Parent?.Role);
        Assert.Equal(PrintRole.TableCell, structure.Paragraphs["td"].Parent?.Role);
        Assert.Equal(PrintRole.Table, structure.Paragraphs["td"].Parent?.Parent?.Parent?.Role);
        Assert.Same(structure.Cells[("t", 1, 0)], structure.Paragraphs["td"].Parent);
    }
}
