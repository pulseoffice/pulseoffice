using PulseOffice.Testing;
using Avalonia.Headless;
using PulseOffice.Drawing.Assets;
using PulseOffice.UI.Controls.Objects;
using Xunit;

namespace PulseOffice.UI.Controls.Tests.Objects;

public sealed class BitmapPixelsVectorTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");

    private sealed class FixedRasterizer : IVectorRasterizer
    {
        public byte[]? Png(byte[] svg, int pixelWidth, int pixelHeight) => BitmapPixelsVectorTests.Png;
    }

    [Fact]
    public Task Un_immagine_solo_SVG_si_decodifica_attraverso_il_rasterizzatore() => Session.Dispatch(() =>
    {
        var svg = System.Text.Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1 1\"><rect width=\"1\" height=\"1\"/></svg>");

        Assert.Null(new BitmapPixels().Decode(svg));
        Assert.NotNull(new BitmapPixels(new FixedRasterizer()).Decode(svg));
    }, CancellationToken.None);
}
