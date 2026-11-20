using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using PulseOffice.Charts;
using PulseOffice.Testing;
using PulseOffice.UI.Controls.Charts.Plots;
using Xunit;

namespace PulseOffice.Sheets.UI.Tests;

public sealed class Round334ParetoRenderingTests
{
    private const int Width = 120;
    private const int Height = 60;

    private static readonly UiThreadSession Session = UiThreadSession.StartNew(typeof(LedgerHeadlessApp));

    [Fact]
    public Task The_categorical_pareto_draws_its_bars_tallest_first() => Session.Dispatch(() =>
    {
        var definition = new ChartDefinition { Id = "p", Type = ChartType.Pareto, SourceSheet = string.Empty, SourceRange = string.Empty };
        var data = new ChartData(["a", "b", "c"], [new ChartSeries("s", [1, 5, 3])]);
        var plot = new Rect(0, 0, Width, Height);
        using var bitmap = new RenderTargetBitmap(new PixelSize(Width, Height));
        using (var context = bitmap.CreateDrawingContext(true))
            new StatisticalPlot().Draw(new ChartFrame(context, definition, data, plot, plot));

        var pixels = Pixels(bitmap);
        Assert.True(Filled(pixels, Width / 6) > Filled(pixels, Width / 2), "the first bar is the tallest");
        Assert.True(Filled(pixels, Width / 2) > Filled(pixels, Width * 5 / 6), "the last bar is the shortest");
    }, CancellationToken.None);

    private static byte[] Pixels(RenderTargetBitmap bitmap)
    {
        var size = Width * Height * 4;
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, Width, Height), buffer, size, Width * 4);
            var pixels = new byte[size];
            Marshal.Copy(buffer, pixels, 0, size);
            return pixels;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int Filled(byte[] pixels, int column) =>
        Enumerable.Range(0, Height).Count(row => pixels[(row * Width + column) * 4 + 3] > 128);
}
