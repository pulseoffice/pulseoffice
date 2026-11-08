using System.Diagnostics;
using PulseOffice.Foundation.Units;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Sheets.Tests;

public class SheetLayoutScaleTests
{
    private const int Rows = 1_000_000;

    [Fact]
    public void AMillionHiddenRowsTakeLittleMemoryAndTime()
    {
        GC.Collect();
        var before = GC.GetTotalMemory(true);
        var clock = Stopwatch.StartNew();

        var layout = new SheetLayout();
        for (var row = 0; row < Rows; row++) layout.HideRow(row);
        var offset = layout.RowOffset(Rows);
        for (var row = 0; row < Rows; row++) layout.ShowRow(row);
        for (var row = 0; row < Rows; row++) layout.HideRow(row);

        clock.Stop();
        var retained = GC.GetTotalMemory(true) - before;
        GC.KeepAlive(layout);

        Assert.Equal(0, offset.Value);
        Assert.Single(layout.RowHeights.Runs);
        Assert.True(layout.IsRowHidden(Rows - 1));
        Assert.False(layout.IsRowHidden(Rows));
        Assert.True(retained < 4 * 1024 * 1024, $"retained {retained} bytes");
        Assert.True(clock.ElapsedMilliseconds < 3000, $"took {clock.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void OffsetsOverManyRunsStayFast()
    {
        var layout = new SheetLayout();
        for (var row = 0; row < 200_000; row += 2) layout.SetRowHeight(row, Twip.FromPoints(30));

        var clock = Stopwatch.StartNew();
        long total = 0;
        for (var row = 0; row < 200_000; row += 7) total += layout.RowOffset(row).Value;
        clock.Stop();

        Assert.Equal(Twip.FromPoints(45).Value, layout.RowOffset(2).Value);
        Assert.Equal(Twip.FromPoints(45).Value * 50_000L, layout.RowOffset(100_000).Value);
        Assert.True(total > 0);
        Assert.True(clock.ElapsedMilliseconds < 1000, $"took {clock.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void AStyleOnEveryRowTakesLittleMemory()
    {
        var layout = new SheetLayout();
        for (var row = 0; row < CellAddress.MaxRows; row++) layout.SetRowStyle(row, 7);

        Assert.Equal(7, layout.DefaultStyleAt(new CellAddress(CellAddress.MaxRows - 1, 3)));
        Assert.Single(layout.RowStyles.Runs);
    }
}
