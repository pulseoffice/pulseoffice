using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Presentations.Tests;

public sealed class FreeAreaPlacementTests
{
    private readonly FreeAreaPlacement _placement = new();
    private readonly DocumentSize _page = new(new Twip(16000), new Twip(9000));
    private readonly DocumentSize _box = new(new Twip(4000), new Twip(1000));

    [Fact]
    public void SuUnaDiapositivaVuotaLaCasellaStaAlCentro()
    {
        var placed = _placement.Place(_page, _box, []);
        Assert.Equal(6000, placed.Left.Value);
        Assert.Equal(4000, placed.Top.Value);
    }

    [Fact]
    public void LaCasellaNonCopreGliOggettiEsistenti()
    {
        var occupied = new DocumentRect(new Twip(2000), new Twip(2000), new Twip(12000), new Twip(5000));
        var placed = _placement.Place(_page, _box, [occupied]);
        Assert.False(placed.IntersectsWith(occupied));
    }
}
