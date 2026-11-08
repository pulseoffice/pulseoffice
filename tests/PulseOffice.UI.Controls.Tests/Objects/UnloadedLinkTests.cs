using PulseOffice.Testing;
using Avalonia;
using Avalonia.Headless;
using PulseOffice.Drawing.Assets;
using PulseOffice.UI.Controls.Shapes;
using Xunit;

namespace PulseOffice.UI.Controls.Tests.Objects;

public sealed class UnloadedLinkTests
{
    private static UiThreadSession Session => MacroEventsViewTests.Session;

    [Fact]
    public Task Un_immagine_collegata_non_ancora_caricata_si_vede_come_segnaposto() => Session.Dispatch(() =>
    {
        var image = new RasterImageObject { Id = "i", Data = Array.Empty<byte>(), MediaType = "image/png", LinkPath = "/tmp/assente/foto.jpg" };
        var recorded = new Avalonia.Media.DrawingGroup();
        using (var context = recorded.Open())
            new DrawingObjectRenderer().Render(context, image, new Rect(0, 0, 120, 80));

        Assert.NotEmpty(recorded.Children);
    }, CancellationToken.None);
}
