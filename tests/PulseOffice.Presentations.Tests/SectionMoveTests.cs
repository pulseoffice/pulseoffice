using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class SectionMoveTests
{
    private static List<Slide> Deck(params string?[] markers) =>
        [.. markers.Select((name, i) => new Slide($"s{i}", SlideLayout.Blank) { SectionName = name })];

    [Fact]
    public void SpostareLaPrimaDiapositivaDiUnaSezioneNonPortaViaLaSezione()
    {
        var after = new SlideSections().MarkersAfterMove(Deck("A", null, "B", null), 0, 3);

        Assert.Equal(["A", "B", null, null], after);
    }

    [Fact]
    public void UnaDiapositivaSpostataInCimaEntraNellaPrimaSezione()
    {
        var after = new SlideSections().MarkersAfterMove(Deck("A", null, "B", null), 3, 0);

        Assert.Equal(["A", null, null, "B"], after);
    }

    [Fact]
    public void UnaDiapositivaSpostataEntraNellaSezioneDiDestinazione()
    {
        var after = new SlideSections().MarkersAfterMove(Deck("A", null, "B", null), 1, 3);

        Assert.Equal(["A", "B", null, null], after);
    }
}
