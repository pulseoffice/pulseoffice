using Avalonia;
using Avalonia.Media.Imaging;
using PulseOffice.Sheets.Engine.Operations;
using PulseOffice.Testing;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round335SparklineRenderingTests
{
    private const int Width = 60;
    private const int Height = 20;

    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(LedgerHeadlessApp));

    private static readonly SparklineStyle Blue = new() { ColorHex = "#0000FF", NegativeHex = "#0000FF", MarkerHex = "#0000FF" };

    private readonly SparklinePainter _painter = new();

    private byte[] Render(SparklinePoints points, SparklineKind kind, SparklineStyle style)
    {
        using var bitmap = new RenderTargetBitmap(new PixelSize(Width, Height), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext(true))
            _painter.Paint(context, new Rect(0, 0, Width, Height), points, kind, style);

        var pixels = new byte[Width * Height * 4];
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(pixels.Length);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, Width, Height), buffer, pixels.Length, Width * 4);
            System.Runtime.InteropServices.Marshal.Copy(buffer, pixels, 0, pixels.Length);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        }
        return pixels;
    }

    private static (byte Red, byte Green, byte Blue) At(byte[] pixels, int x, int y)
    {
        var offset = (y * Width + x) * 4;
        return (pixels[offset + 2], pixels[offset + 1], pixels[offset]);
    }

    private static bool IsBlue(byte[] pixels, int x, int y) => At(pixels, x, y) is { Blue: > 180, Red: < 80, Green: < 80 };

    [Fact]
    public Task Date_positions_move_a_column_away_from_the_even_slot() => Session.Dispatch(() =>
    {
        var even = new SparklinePoints([1.0, 1.0, 1.0], 0, 1);
        var dated = even with { Positions = [0, 0.9, 1] };

        Assert.True(IsBlue(Render(even, SparklineKind.Column, Blue), 30, 10));
        Assert.False(IsBlue(Render(dated, SparklineKind.Column, Blue), 30, 10));
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task Right_to_left_draws_the_first_value_at_the_right_edge() => Session.Dispatch(() =>
    {
        var points = new SparklinePoints([1.0, 0.0, 0.0], 0, 1);

        var forward = Render(points, SparklineKind.Column, Blue);
        var mirrored = Render(points with { RightToLeft = true }, SparklineKind.Column, Blue);

        Assert.True(IsBlue(forward, 10, 10));
        Assert.False(IsBlue(forward, 48, 10));
        Assert.True(IsBlue(mirrored, 48, 10));
        Assert.False(IsBlue(mirrored, 10, 10));
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task The_axis_is_drawn_at_zero_in_its_own_colour_only_when_asked() => Session.Dispatch(() =>
    {
        var points = new SparklinePoints([-1.0, 1.0], -1, 1);
        var axis = Blue with { ShowAxis = true, AxisHex = "#00FF00", LineWidth = 1 };

        static bool Green(byte[] pixels) =>
            Enumerable.Range(9, 3).Any(y => At(pixels, 8, y) is { Green: > 100, Red: < 80, Blue: < 80 });

        Assert.True(Green(Render(points, SparklineKind.Line, axis)));
        Assert.False(Green(Render(points, SparklineKind.Line, axis with { ShowAxis = false })));
        return Task.CompletedTask;
    }, CancellationToken.None);

    [Fact]
    public Task High_and_low_columns_take_their_own_colours() => Session.Dispatch(() =>
    {
        var points = new SparklinePoints([1.0, 2.0, 3.0], 0, 3);
        var style = Blue with { ShowHighPoint = true, HighHex = "#FF0000", ShowLowPoint = true, LowHex = "#00FF00" };

        var pixels = Render(points, SparklineKind.Column, style);

        Assert.True(At(pixels, 10, 16) is { Green: > 180, Red: < 80, Blue: < 80 });
        Assert.True(IsBlue(pixels, 30, 16));
        Assert.True(At(pixels, 48, 16) is { Red: > 180, Green: < 80, Blue: < 80 });
        return Task.CompletedTask;
    }, CancellationToken.None);
}
