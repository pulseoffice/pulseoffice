using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round333SparklineTests
{
    [Fact]
    public void Every_sparkline_group_option_crosses_ods()
    {
        var read = RoundTrip(Round333SparklineSample.Workbook());
        var group = Assert.Single(read.Sheets[0].Sparklines.Groups);

        Round333SparklineSample.AssertSame(group);
    }

    private static Workbook RoundTrip(Workbook source)
    {
        using var stream = new MemoryStream();
        new OdsWriter().Write(source, stream);
        stream.Position = 0;
        var result = new OdsReader().Read(stream);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        return result.Value!;
    }
}

internal static class Round333SparklineSample
{
    public static Workbook Workbook()
    {
        var workbook = new Workbook();
        var host = workbook.AddSheet("Host");
        workbook.AddSheet("Other data");
        host.Sparklines.Add(new SparklineGroup
        {
            Id = "g",
            Sparklines = [new Sparkline(new CellAddress(0, 4), Range(0, 0, 0, 3), "Other data")],
            Kind = SparklineKind.Line,
            MinimumAxis = SparklineAxisBound.Custom,
            ManualMinimum = -2.5,
            MaximumAxis = SparklineAxisBound.Group,
            EmptyCells = SparklineEmptyCells.Span,
            ShowHidden = true,
            RightToLeft = true,
            DateRange = Range(1, 0, 1, 3),
            Style = new SparklineStyle
            {
                ShowMarkers = true,
                ShowAxis = true,
                AxisHex = "#112233",
                HighHex = "#00AA00",
                LowHex = "#AA0000",
                FirstHex = "#0000AA",
                LastHex = "#AAAA00",
            },
        });
        return workbook;
    }

    public static void AssertSame(SparklineGroup group)
    {
        var sparkline = Assert.Single(group.Sparklines);
        Assert.Equal("Other data", sparkline.SourceSheet);
        Assert.Equal(Range(0, 0, 0, 3), sparkline.Source);
        Assert.Equal(SparklineAxisBound.Custom, group.MinimumAxis);
        Assert.Equal(-2.5, group.ManualMinimum);
        Assert.Equal(SparklineAxisBound.Group, group.MaximumAxis);
        Assert.Null(group.ManualMaximum);
        Assert.Equal(SparklineEmptyCells.Span, group.EmptyCells);
        Assert.True(group.ShowHidden);
        Assert.True(group.RightToLeft);
        Assert.Equal(Range(1, 0, 1, 3), group.DateRange);
        Assert.Null(group.DateSheet);
        Assert.True(group.Style.ShowMarkers);
        Assert.True(group.Style.ShowAxis);
        Assert.Equal("#112233", group.Style.AxisHex);
        Assert.Equal("#00AA00", group.Style.HighHex);
        Assert.Equal("#AA0000", group.Style.LowHex);
        Assert.Equal("#0000AA", group.Style.FirstHex);
        Assert.Equal("#AAAA00", group.Style.LastHex);
    }

    private static CellRange Range(int top, int left, int bottom, int right) =>
        new(new CellAddress(top, left), new CellAddress(bottom, right));
}
