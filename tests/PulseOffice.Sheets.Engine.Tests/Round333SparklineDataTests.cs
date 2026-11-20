using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Sheets.Engine.Tests;

public sealed class Round333SparklineDataTests(EngineFixture fixture) : IClassFixture<EngineFixture>
{
    private static readonly CellRange Row = new(new CellAddress(0, 0), new CellAddress(0, 3));

    private readonly SparklineData _data = new();

    private static Worksheet Sheet(params double?[] values)
    {
        var sheet = new Worksheet("Data");
        for (var index = 0; index < values.Length; index++)
            if (values[index] is { } value) sheet.SetValue(new CellAddress(0, index), CellValue.Number(value));
        return sheet;
    }

    private static SparklineGroup Group(Sparkline sparkline) => new() { Id = "g", Sparklines = [sparkline] };

    [Fact]
    public void A_line_scales_from_its_lowest_value_not_from_zero()
    {
        var line = new Sparkline(new CellAddress(0, 5), Row);

        var points = _data.Read(Sheet(100, 110, 105, 108), Group(line), line);

        Assert.Equal(100, points.Minimum);
        Assert.Equal(110, points.Maximum);
    }

    [Fact]
    public void Empty_cells_read_as_zero_only_when_the_group_says_so()
    {
        var line = new Sparkline(new CellAddress(0, 5), Row);
        var sheet = Sheet(1, null, 3, 4);

        Assert.Null(_data.Read(sheet, Group(line), line).Values[1]);
        Assert.Equal(0, _data.Read(sheet, Group(line) with { EmptyCells = SparklineEmptyCells.Zero }, line).Values[1]);
        Assert.True(_data.Read(sheet, Group(line) with { EmptyCells = SparklineEmptyCells.Span }, line).SpansGaps);
    }

    [Fact]
    public void Hidden_columns_are_left_out_unless_shown()
    {
        var line = new Sparkline(new CellAddress(0, 5), Row);
        var sheet = Sheet(1, 2, 3, 4);
        sheet.Layout.HideColumn(1);

        Assert.Equal([1d, 3d, 4d], _data.Read(sheet, Group(line), line).Values);
        Assert.Equal(4, _data.Read(sheet, Group(line) with { ShowHidden = true }, line).Values.Count);
    }

    [Fact]
    public void Custom_bounds_replace_the_data_bounds()
    {
        var line = new Sparkline(new CellAddress(0, 5), Row);
        var group = Group(line) with
        {
            MinimumAxis = SparklineAxisBound.Custom,
            ManualMinimum = -10,
            MaximumAxis = SparklineAxisBound.Custom,
            ManualMaximum = 10,
        };

        var points = _data.Read(Sheet(1, 2, 3, 4), group, line);

        Assert.Equal(-10, points.Minimum);
        Assert.Equal(10, points.Maximum);
    }

    [Fact]
    public void A_date_axis_spaces_points_by_their_dates_and_right_to_left_mirrors_them()
    {
        var line = new Sparkline(new CellAddress(0, 5), Row);
        var sheet = Sheet(1, 2, 3, 4);
        foreach (var (column, date) in new[] { (0, 0d), (1, 1d), (2, 2d), (3, 10d) })
            sheet.SetValue(new CellAddress(1, column), CellValue.Number(date));
        var group = Group(line) with { DateRange = new CellRange(new CellAddress(1, 0), new CellAddress(1, 3)) };

        var points = _data.Read(sheet, group, line);
        var mirrored = _data.Read(sheet, group with { RightToLeft = true }, line);

        Assert.Equal(0.2, points.Position(2), 6);
        Assert.Equal(0.8, mirrored.Position(2), 6);
    }

    [Fact]
    public void A_source_on_another_sheet_is_read_and_follows_its_rename()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var host = workbook.Sheets[0];
        var other = workbook.AddSheet("Source");
        other.SetValue(new CellAddress(0, 0), CellValue.Number(7));
        var line = new Sparkline(new CellAddress(0, 5), new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)), "Source");
        host.Sparklines.Add(Group(line));

        Assert.Equal([7d], _data.Read(host, host.Sparklines.Groups[0], line).Values);

        var renaming = new SheetRenaming();
        var plan = renaming.Plan(workbook, other, "Renamed");
        renaming.Apply(engine, plan);

        Assert.Equal("Renamed", host.Sparklines.Groups[0].Sparklines[0].SourceSheet);

        renaming.Revert(engine, plan);

        Assert.Equal("Source", host.Sparklines.Groups[0].Sparklines[0].SourceSheet);
    }

    [Fact]
    public void Inserting_rows_on_the_host_leaves_a_foreign_source_alone()
    {
        var (workbook, engine) = fixture.CreateWorkbook();
        var host = workbook.Sheets[0];
        workbook.AddSheet("Source");
        var line = new Sparkline(new CellAddress(5, 5), Row, "Source");
        host.Sparklines.Add(Group(line));

        new SheetStructure().InsertRows(engine, 0, 0, 2);

        var moved = host.Sparklines.Groups[0].Sparklines[0];
        Assert.Equal(new CellAddress(7, 5), moved.Cell);
        Assert.Equal(Row, moved.Source);
        Assert.Equal("Source", moved.SourceSheet);
    }
}
