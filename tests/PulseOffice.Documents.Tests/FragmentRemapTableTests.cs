using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Clipboard;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Tests;

public sealed class FragmentRemapTableTests
{
    private readonly Fixture _f = new();

    [Fact]
    public void LaRimappaturaConservaFormatoNumericoProtezioneEFormattazioneDirettaDellaTabella()
    {
        var cell = new TableCell(new BlockContainer([new ParagraphBlock("p900", "12")]))
        {
            Protected = true,
            NumberFormat = new CellNumberFormat(CellValueKind.Currency, 2) { Symbol = "€" },
        };
        var table = new TableBlock("t900", [new TableRow([cell])], [new Twip(2000)])
        {
            Direct = new TableProperties { Background = 0xFFFF0000 },
            PageStyleName = DefaultStyleNames.Landscape,
            PageBreakBefore = true,
        };
        var fragment = new DocumentFragment(new BlockContainer([table]), _f.Document.Styles.Clone(), [], [], []);

        var remapped = (TableBlock)new FragmentRemapper(_f.Stories, _f.Tree).Remap(_f.Document, fragment).Blocks[0];

        var copy = remapped.Rows[0].Cells[0];
        Assert.True(copy.Protected);
        Assert.Equal(cell.NumberFormat, copy.NumberFormat);
        Assert.Equal(table.Direct, remapped.Direct);
        Assert.Equal(DefaultStyleNames.Landscape, remapped.PageStyleName);
        Assert.True(remapped.PageBreakBefore);
    }
}
