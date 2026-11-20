using Avalonia;
using PulseOffice.Foundation.Windowing;
using Xunit;

namespace PulseOffice.Shell.Tests;

public sealed class Round314TornOffWindowSizingTests
{
    private readonly TornOffWindowSizing _sizing = new();
    private static readonly Size Minimum = new(720, 480);

    [Fact]
    public void Without_a_saved_size_the_torn_off_window_takes_the_source_window_size() =>
        Assert.Equal(new Size(1000, 700), _sizing.Choose(null, 2, new Size(1000, 700), Minimum, new Size(1600, 900)));

    [Fact]
    public void A_saved_size_in_pixels_is_restored_in_device_independent_units() =>
        Assert.Equal(new Size(900, 600),
            _sizing.Choose(new WindowPlacement(10, 10, 1800, 1200, false), 2, new Size(1000, 700), Minimum, null));

    [Fact]
    public void The_size_never_falls_below_the_minimum_nor_exceeds_the_screen() =>
        Assert.Equal(new Size(1280, 480),
            _sizing.Choose(new WindowPlacement(0, 0, 4000, 100, false), 1, new Size(1000, 700), Minimum, new Size(1280, 800)));

    [Fact]
    public void A_torn_off_window_closed_maximized_reopens_maximized_with_its_normal_bounds()
    {
        var normal = new WindowPlacement(10, 20, 900, 600, false);
        var saved = _sizing.Remember(normal, new WindowPlacement(0, 0, 1920, 1080, false), maximized: true);

        Assert.Equal(normal with { Maximized = true }, saved);
        Assert.True(_sizing.OpensMaximized(saved));
        Assert.Equal(new Size(900, 600), _sizing.Choose(saved, 1, new Size(1000, 700), Minimum, null));
    }

    [Fact]
    public void A_torn_off_window_closed_normal_reopens_normal()
    {
        var current = new WindowPlacement(5, 5, 800, 500, true);
        var saved = _sizing.Remember(null, current, maximized: false);

        Assert.False(saved.Maximized);
        Assert.False(_sizing.OpensMaximized(saved));
        Assert.False(_sizing.OpensMaximized(null));
    }
}
