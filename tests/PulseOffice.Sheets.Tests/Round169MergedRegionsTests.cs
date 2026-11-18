using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public sealed class Round169MergedRegionsTests
{
    private static CellRange Pair(int row, int column) =>
        new(new CellAddress(row, column), new CellAddress(row, column + 1));

    [Fact]
    public void Many_side_by_side_merges_in_a_few_rows_are_added_and_found_in_linear_time()
    {
        var merges = new MergedRegions();
        new PulseOffice.Testing.GrowthCheck().Linear(count =>
        {
            merges = new MergedRegions();
            var current = merges;
            return () =>
            {
                for (var index = 0; index < count; index++)
                    current.Add(Pair(index % 4, index / 4 * 2));
                for (var index = 0; index < count; index++)
                    Assert.NotNull(current.At(new CellAddress(index % 4, index / 4 * 2 + 1)));
            };
        }, 20_000);

        Assert.Equal(20_000, merges.Count);
        Assert.Equal(Pair(3, 6), merges.At(new CellAddress(3, 7)));
        Assert.Null(merges.At(new CellAddress(4, 7)));
        Assert.Equal(2, merges.Overlapping(new CellRange(new CellAddress(0, 1), new CellAddress(0, 2))).Count());
    }

    [Fact]
    public void Regions_of_every_shape_keep_their_insertion_order_and_are_all_found()
    {
        var merges = new MergedRegions();
        var tall = new CellRange(new CellAddress(0, 10), new CellAddress(CellAddress.MaxRows - 1, 11));
        var wide = new CellRange(new CellAddress(5, 0), new CellAddress(6, CellAddress.MaxColumns - 1));
        var huge = new CellRange(new CellAddress(100, 100), new CellAddress(900_000, 9_000));
        var small = Pair(1, 1);
        merges.Add(small);
        merges.Add(tall);
        merges.Add(huge);

        Assert.Equal([small, tall, huge], merges.All);
        Assert.Equal(huge, merges.At(new CellAddress(800_000, 8_000)));
        Assert.Equal(tall, merges.At(new CellAddress(700_000, 11)));

        merges.Add(wide);

        Assert.Equal([small, huge, wide], merges.All);
        Assert.Equal(wide, merges.At(new CellAddress(6, 16_000)));
        Assert.Null(merges.At(new CellAddress(700_000, 11)));
        Assert.Equal([huge, wide], merges.Overlapping(new CellRange(new CellAddress(6, 50), new CellAddress(200, 150))));
    }
}
