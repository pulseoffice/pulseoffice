using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round335SparklineGroupsTests
{
    private readonly SparklineGroups _groups = new(new SheetRangeReferences());

    private static CellRange Range(int row, int column, int lastRow, int lastColumn) =>
        new(new CellAddress(row, column), new CellAddress(lastRow, lastColumn));

    private static SparklineGroup One(string id, int row) =>
        new() { Id = id, Sparklines = [new Sparkline(new CellAddress(row, 5), Range(row, 0, row, 3))], Kind = SparklineKind.Column };

    [Fact]
    public void Laying_out_data_on_another_sheet_names_that_sheet_and_the_dates()
    {
        var workbook = Workbook.CreateDefault();
        var source = workbook.AddSheet("Other Data");
        for (var row = 0; row < 2; row++)
            for (var column = 0; column < 3; column++) source.SetValue(new CellAddress(row, column), CellValue.Number(row + column));

        var group = _groups.Lay(workbook, new SparklineLayoutRequest(
            0, new SheetRangeReference(1, Range(0, 0, 1, 2)), Range(0, 4, 1, 4),
            new SheetRangeReference(1, Range(5, 0, 5, 2)), new SparklineGroup { Id = "g", Sparklines = [] }));

        Assert.NotNull(group);
        Assert.Equal(2, group.Sparklines.Count);
        Assert.All(group.Sparklines, line => Assert.Equal("Other Data", line.SourceSheet));
        Assert.Equal(Range(1, 0, 1, 2), group.Sparklines[1].Source);
        Assert.Equal("Other Data", group.DateSheet);

        var text = _groups.Describe(workbook, 0, group);
        Assert.Equal("'Other Data'.A1:C2", text.Data);
        Assert.Equal("E1:E2", text.Destination);
        Assert.Equal("'Other Data'.A6:C6", text.Dates);
    }

    [Fact]
    public void Grouping_takes_the_options_of_the_group_at_the_active_cell()
    {
        var sparklines = new SparklineCollection();
        sparklines.Add(One("a", 0));
        sparklines.Add(One("b", 1) with { Kind = SparklineKind.Stacked });

        var grouped = _groups.Grouped(sparklines, Range(0, 5, 1, 5), new CellAddress(1, 5), "merged");

        var merged = Assert.Single(grouped!);
        Assert.Equal(SparklineKind.Stacked, merged.Kind);
        Assert.Equal(2, merged.Sparklines.Count);
        Assert.Null(_groups.Grouped(sparklines, Range(0, 5, 0, 5), new CellAddress(0, 5), "x"));
    }

    [Fact]
    public void Ungrouping_gives_each_sparkline_its_own_group_with_the_same_options()
    {
        var sparklines = new SparklineCollection();
        sparklines.Add(One("a", 0) with { Sparklines = [.. One("a", 0).Sparklines, .. One("a", 1).Sparklines] });

        var split = _groups.Ungrouped(sparklines, Range(0, 5, 0, 5));

        Assert.Equal(2, split!.Count);
        Assert.All(split, group => Assert.Equal(SparklineKind.Column, group.Kind));
        Assert.Equal(2, split.Select(group => group.Id).Distinct().Count());
        Assert.Null(_groups.Ungrouped(new SparklineCollection(), Range(0, 5, 0, 5)));
    }
}
