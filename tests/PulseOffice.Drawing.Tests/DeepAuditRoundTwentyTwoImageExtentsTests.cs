using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class DeepAuditRoundTwentyTwoImageExtentsTests
{
    private readonly ImageExtents _extents = new();

    private static ImageBounds Page => new(new DocumentSize(Twip.FromCentimeters(16), Twip.FromCentimeters(24)), Twip.FromCentimeters(1));

    [Theory]
    [InlineData(1, int.MaxValue)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(0, 0)]
    [InlineData(-5, 30)]
    public void LeDimensioniDichiarateEstremeRestanoNelRiquadro(int width, int height)
    {
        var size = _extents.Natural(width, height, Page);

        Assert.InRange(size.Width.Value, 1, Page.Largest.Width.Value);
        Assert.InRange(size.Height.Value, 1, Page.Largest.Height.Value);
    }

    [Fact]
    public void UnImmaginePiccolaConservaLaDimensioneA96Punti()
    {
        var size = _extents.Natural(96, 48, new ImageBounds(new DocumentSize(new Twip(100_000), new Twip(100_000)), default));

        Assert.Equal(new DocumentSize(new Twip(1440), new Twip(720)), size);
    }

    [Fact]
    public void UnImmagineStrettaSiAllargaAlMinimoConservandoLeProporzioni()
    {
        var size = _extents.Natural(10, 20, Page);

        Assert.Equal(Twip.FromCentimeters(1).Value, size.Width.Value);
        Assert.InRange(size.Height.Value, Twip.FromCentimeters(2).Value - 1, Twip.FromCentimeters(2).Value + 1);
    }
}
