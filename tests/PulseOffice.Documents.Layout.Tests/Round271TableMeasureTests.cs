using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round271TableMeasureTests
{
    private static readonly LayoutFixture Fixture = new();

    private const string Long = "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz";

    private readonly ITableMeasure _measure = Fixture.Services.GetRequiredService<ITableMeasure>();

    private static TableCell Cell(Block block) => new(new BlockContainer([block]));

    private static TableBlock Narrow(string id, params TableRow[] extra) =>
        new(id, [new TableRow([Cell(new ParagraphBlock(id + "a", "a")), Cell(new ParagraphBlock(id + "b", "b"))]), .. extra], [new Twip(200), new Twip(200)]) { AutoWidth = true };

    [Fact]
    public void A_spanning_cell_widens_the_columns_it_spans()
    {
        var plain = _measure.Widths(Fixture.Document, Narrow("p"), TableMeasureKind.Optimal);
        var wide = new TableCell(new BlockContainer([new ParagraphBlock("sw", Long)])) { ColumnSpan = 2 };
        var covered = new TableCell(new BlockContainer([new ParagraphBlock("sc", "")])) { Covered = true };
        var spanned = _measure.Widths(Fixture.Document, Narrow("s", new TableRow([wide, covered])), TableMeasureKind.Optimal);

        Assert.True(spanned[0] > plain[0] && spanned[1] > plain[1]);
        Assert.True(spanned.Sum() > _measure.Widths(Fixture.Document, new TableBlock("l", [new TableRow([Cell(new ParagraphBlock("l1", Long))])], [new Twip(20)]) { AutoWidth = true }, TableMeasureKind.Optimal)[0] - 1);
    }

    [Fact]
    public void A_nested_automatic_table_counts_with_its_measured_width()
    {
        var nested = new TableBlock("n", [new TableRow([Cell(new ParagraphBlock("n1", Long))])], [new Twip(20)]) { AutoWidth = true };
        var inner = _measure.Widths(Fixture.Document, nested, TableMeasureKind.Optimal).Sum();
        var outer = new TableBlock("o", [new TableRow([Cell(nested)])], [new Twip(20)]) { AutoWidth = true };

        Assert.True(_measure.Widths(Fixture.Document, outer, TableMeasureKind.Optimal)[0] > inner);
    }
}
