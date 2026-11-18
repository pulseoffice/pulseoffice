using PulseOffice.Sheets.Styling;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round228CellLinkClickTests
{
    private readonly ICellLinkHitArea _area = new CellLinkHitArea();

    [Fact]
    public void Left_aligned_text_is_hit_only_over_its_width()
    {
        var span = _area.Span(0, 100, 30, 3, CellHorizontalAlignment.Left, alignsRight: false, wraps: false);

        Assert.True(span.Contains(10));
        Assert.False(span.Contains(60));
    }

    [Fact]
    public void Right_and_centred_text_move_the_hit_area()
    {
        var right = _area.Span(0, 100, 30, 3, CellHorizontalAlignment.General, alignsRight: true, wraps: false);
        var centred = _area.Span(0, 100, 30, 3, CellHorizontalAlignment.Center, alignsRight: false, wraps: false);

        Assert.False(right.Contains(10));
        Assert.True(right.Contains(90));
        Assert.True(centred.Contains(50));
        Assert.False(centred.Contains(10));
    }

    [Fact]
    public void Wrapped_or_overfull_text_covers_the_whole_cell()
    {
        Assert.True(_area.Span(0, 100, 30, 3, CellHorizontalAlignment.Left, false, wraps: true).Contains(95));
        Assert.True(_area.Span(0, 100, 99, 3, CellHorizontalAlignment.Left, false, wraps: false).Contains(99));
    }

    [Fact]
    public void The_setting_is_on_by_default()
    {
        var setting = Assert.Single(new LedgerLinkSettings().GetSettings());

        Assert.Equal(LedgerLinkSettings.FollowOnClick, setting.Key);
        Assert.True(setting.DefaultValue is true);
    }
}
