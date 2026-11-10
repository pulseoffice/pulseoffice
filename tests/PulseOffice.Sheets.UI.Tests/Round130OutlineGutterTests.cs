using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Sheets.Engine.Evaluation.Functions;
using PulseOffice.Sheets.Engine.Recalculation;
using PulseOffice.Sheets.UI.GridParts;
using PulseOffice.Sheets.UI.Outline;
using PulseOffice.UI.Theming;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round130OutlineGutterTests
{
    private static (Workbook Workbook, GridViewport Viewport) Grid(OutlineSummary summary)
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        sheet.RowGroups.Add(new OutlineGroup { First = 3, Last = 5 });
        sheet.ColumnGroups.Add(new OutlineGroup { First = 2, Last = 3 });
        sheet.OutlineSummary = summary;
        var engine = new ServiceCollection().AddSheetsEngine().BuildServiceProvider()
            .GetRequiredService<ICalculationEngineFactory>().Create(workbook);
        return (workbook, new GridViewport(engine, DesignTokens.For(InterfaceDensity.Compact), 0, () => new Size(1200, 800), new OutlineGutter()));
    }

    private static Point RowPoint(GridViewport viewport, int row) =>
        new(viewport.Gutter.SlotOffset(1) + viewport.Gutter.SlotSize / 2,
            viewport.Geometry.ColumnHeaderHeight + viewport.Geometry.RowOffset(row) + viewport.Geometry.RowHeight(row) / 2);

    private static Point ColumnPoint(GridViewport viewport, int column) =>
        new(viewport.Geometry.RowHeaderWidth + viewport.Geometry.ColumnOffset(column) + viewport.Geometry.ColumnWidth(column) / 2,
            viewport.Gutter.SlotOffset(1) + viewport.Gutter.SlotSize / 2);

    [Fact]
    public void The_headers_make_room_for_the_outline_gutter()
    {
        var (_, viewport) = Grid(OutlineSummary.Default);
        var geometry = viewport.Geometry;

        Assert.Equal(viewport.Gutter.Thickness(viewport.Sheet!, StructureAxis.Rows), geometry.RowGutterWidth);
        Assert.True(geometry.RowGutterWidth > 0);
        Assert.True(geometry.ColumnGutterHeight > 0);
        Assert.Equal(20 + geometry.ColumnGutterHeight, geometry.ColumnHeaderHeight);
    }

    [Fact]
    public void With_the_summary_below_the_button_sits_after_the_group()
    {
        var (_, viewport) = Grid(OutlineSummary.Default);

        Assert.Equal(new OutlineButton(StructureAxis.Rows, OutlineButtonKind.Toggle, 1) { Index = 6, GroupIndex = 0, SpanFirst = 3, SpanLast = 5 },
            viewport.OutlineButtonAt(RowPoint(viewport, 6)));
        Assert.Null(viewport.OutlineButtonAt(RowPoint(viewport, 2)));
        Assert.Equal(4, viewport.OutlineButtonAt(ColumnPoint(viewport, 4))?.Index);
        Assert.Null(viewport.OutlineButtonAt(ColumnPoint(viewport, 1)));
    }

    [Fact]
    public void With_the_summary_above_the_button_sits_before_the_group()
    {
        var (_, viewport) = Grid(new OutlineSummary(Below: false, Right: false));

        Assert.Equal(2, viewport.OutlineButtonAt(RowPoint(viewport, 2))?.Index);
        Assert.Null(viewport.OutlineButtonAt(RowPoint(viewport, 6)));
        Assert.Equal(1, viewport.OutlineButtonAt(ColumnPoint(viewport, 1))?.Index);
        Assert.Null(viewport.OutlineButtonAt(ColumnPoint(viewport, 4)));
    }

    [Fact]
    public void Pressing_the_toggle_collapses_and_expands_the_group()
    {
        var (workbook, viewport) = Grid(new OutlineSummary(Below: false));
        var sheet = workbook.Sheets[0];
        var button = viewport.OutlineButtonAt(RowPoint(viewport, 2))!;

        viewport.Gutter.Press(sheet, button);
        Assert.True(sheet.Layout.IsRowHidden(4));
        Assert.False(sheet.Layout.IsRowHidden(2));
        Assert.True(viewport.OutlineButtonAt(RowPoint(viewport, 2))!.Collapsed);

        viewport.Gutter.Press(sheet, viewport.OutlineButtonAt(RowPoint(viewport, 2))!);
        Assert.False(sheet.Layout.IsRowHidden(4));
    }

    [Fact]
    public void The_level_buttons_sit_in_the_corner_and_show_a_level()
    {
        var (workbook, viewport) = Grid(OutlineSummary.Default);
        var geometry = viewport.Geometry;
        var sheet = workbook.Sheets[0];
        var levelOne = new Point(viewport.Gutter.SlotOffset(1) + 3, geometry.ColumnGutterHeight + 5);

        var button = viewport.OutlineButtonAt(levelOne);
        Assert.Equal(new OutlineButton(StructureAxis.Rows, OutlineButtonKind.Level, 1), button);

        viewport.Gutter.Press(sheet, button!);
        Assert.True(sheet.Layout.IsRowHidden(3));
        viewport.Gutter.Press(sheet, new OutlineButton(StructureAxis.Rows, OutlineButtonKind.Level, 2));
        Assert.False(sheet.Layout.IsRowHidden(3));
    }
}
