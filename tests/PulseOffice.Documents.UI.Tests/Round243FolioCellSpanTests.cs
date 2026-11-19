using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Fields;
using PulseOffice.Documents.UI.Rendering;
using PulseOffice.Foundation.Units;
using PulseOffice.Printing;
using Xunit;

namespace PulseOffice.Documents.UI.Tests;

public sealed class Round243FolioCellSpanTests
{
    private sealed class NoOutline : IDocumentOutline
    {
        public int? LevelOf(TextDocument document, ParagraphBlock paragraph) => null;

        public IReadOnlyList<OutlineEntry> Headings(TextDocument document, int maximumLevel = 10) => [];
    }

    [Fact]
    public void Merged_cells_carry_their_column_and_row_spans()
    {
        var document = new TextDocument();
        var merged = new TableCell(new BlockContainer([new ParagraphBlock("m", "Unito")])) { ColumnSpan = 2, RowSpan = 3 };
        document.Body.Add(new TableBlock("t", [new TableRow([merged])], [Twip.FromCentimeters(5)]));

        var cell = new FolioStructureIndex(new NoOutline()).Index(document).Cells[("t", 0, 0)];

        Assert.Equal(2, cell.ColumnSpan);
        Assert.Equal(3, cell.RowSpan);
    }
}
