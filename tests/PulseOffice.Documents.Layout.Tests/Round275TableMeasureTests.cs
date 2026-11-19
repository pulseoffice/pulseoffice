using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Layout.Flow;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Documents.Layout.Tests;

public sealed class Round275TableMeasureTests
{
    private static readonly LayoutFixture Fixture = new();

    private readonly ITableMeasure _measure = Fixture.Services.GetRequiredService<ITableMeasure>();

    [Fact]
    public void A_nested_percent_table_counts_with_its_measured_width()
    {
        var nested = new TableBlock("n", [new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("n1", "abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz")]))])], [new Twip(20)]) { WidthPercent = 100 };
        var inner = _measure.Widths(Fixture.Document, nested, TableMeasureKind.Optimal).Sum();
        var outer = new TableBlock("o", [new TableRow([new TableCell(new BlockContainer([nested]))])], [new Twip(20)]) { AutoWidth = true };

        Assert.True(inner > 10);
        Assert.True(_measure.Widths(Fixture.Document, outer, TableMeasureKind.Optimal)[0] > inner);
    }
}
