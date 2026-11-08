using PulseOffice.Foundation.Network;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeepAuditExternalAccessRound21Tests
{
    private readonly IDeckMediaFiles _media = new DeckMediaFiles(new PulseOffice.Platform.ExecutableFileTypes(), new NetworkSharePaths());

    private static MediaElement Linked(string link) => new()
    {
        Id = "m",
        Kind = MediaKind.Video,
        Data = [],
        MimeType = "video/mp4",
        LinkedPath = link,
    };

    [Theory]
    [InlineData(@"\\attacker.example\share\intro.mp4")]
    [InlineData("//attacker.example/share/intro.mp4")]
    [InlineData("file://attacker.example/share/intro.mp4")]
    public void A_linked_medium_on_a_network_share_is_blocked_without_probing_it(string link)
    {
        Assert.IsType<BlockedMediaFile>(_media.Source(Linked(link), "/tmp/deck.odp"));
    }
}
