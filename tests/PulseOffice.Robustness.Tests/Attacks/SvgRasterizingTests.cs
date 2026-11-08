using System.Net;
using System.Net.Sockets;
using System.Text;
using PulseOffice.Formats.Rasterizing;
using Xunit;

namespace PulseOffice.Robustness.Tests.Attacks;

public sealed class SvgRasterizingTests
{
    private readonly SkiaVectorRasterizer _rasterizer = new();

    [Fact]
    public async Task An_svg_image_never_fetches_an_external_address()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var accepted = listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken).AsTask();

        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="10" height="10">
              <image width="10" height="10" xlink:href="http://127.0.0.1:{port}/leak.png"/>
              <image width="10" height="10" href="http://127.0.0.1:{port}/leak2.png"/>
              <use xlink:href="http://127.0.0.1:{port}/sprite.svg#a"/>
              <rect width="10" height="10" fill="url(http://127.0.0.1:{port}/fill.svg#p)"/>
              <script>fetch('http://127.0.0.1:{port}/script')</script>
            </svg>
            """;

        _rasterizer.Png(Encoding.UTF8.GetBytes(svg), 10, 10);

        var connected = await Task.WhenAny(accepted, Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken));
        Assert.NotSame(accepted, connected);
    }

    [Fact]
    public void An_svg_image_never_embeds_a_local_file()
    {
        var secret = Path.Combine(Path.GetTempPath(), $"pulse-secret-{Guid.NewGuid():N}.png");
        using (var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(10, 10)))
        {
            surface.Canvas.Clear(SkiaSharp.SKColors.Red);
            using var image = surface.Snapshot();
            using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(secret, data.ToArray());
        }

        try
        {
            var svg = $"""
                <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="10" height="10">
                  <rect width="10" height="10" fill="white"/>
                  <image width="10" height="10" xlink:href="{new Uri(secret).AbsoluteUri}"/>
                  <image width="10" height="10" xlink:href="{secret}"/>
                </svg>
                """;

            var png = _rasterizer.Png(Encoding.UTF8.GetBytes(svg), 10, 10);

            Assert.NotNull(png);
            using var bitmap = SkiaSharp.SKBitmap.Decode(png);
            Assert.Equal(SkiaSharp.SKColors.White, bitmap.GetPixel(5, 5));
        }
        finally
        {
            File.Delete(secret);
        }
    }

    [Fact]
    public void An_svg_with_entity_expansion_or_external_entities_is_refused()
    {
        var svg = """
            <?xml version="1.0"?>
            <!DOCTYPE svg [
              <!ENTITY a "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa">
              <!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;">
              <!ENTITY c "&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;">
              <!ENTITY d "&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;&c;">
              <!ENTITY e "&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;&d;">
              <!ENTITY f "&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;&e;">
              <!ENTITY x SYSTEM "file:///etc/passwd">
            ]>
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><text>&f;&x;</text></svg>
            """;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var png = _rasterizer.Png(Encoding.UTF8.GetBytes(svg), 10, 10);

        Assert.Null(png);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
    }
}
