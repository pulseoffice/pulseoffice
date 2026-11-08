using PulseOffice.Drawing.Freeform;
using PulseOffice.Drawing.Layout;
using PulseOffice.Drawing.Shapes;
using Xunit;

namespace PulseOffice.Drawing.Tests;

public sealed class AuditRegressionTests
{
    [Fact]
    public void LaDistribuzioneUsaIlBordoPiuLontanoNonLUltimoInizio()
    {
        var boxes = new[]
        {
            new DistributedBox("a", 0, 10),
            new DistributedBox("b", 20, 100),
            new DistributedBox("c", 30, 10),
        };

        var result = new BoxDistribution().EqualGaps(boxes);

        var end = result.Max(box => box.Start + box.Size);
        Assert.Equal(120, end, 6);
        Assert.Equal(0, result[0].Start, 6);
    }

    [Fact]
    public void LaMisuraUsaGliEstremiVeriDellaCubica()
    {
        var path = new FreeformPath([new PathFigure([
            new PathNode(new ShapePoint(0, 0)) { After = new ShapePoint(0, 10) },
            new PathNode(new ShapePoint(1, 0)) { Before = new ShapePoint(1, 10) },
        ])]);

        var bounds = new PathNormalizer().Measure(path);

        Assert.Equal(7.5, bounds.Height, 6);
    }

    [Fact]
    public void LaDistribuzioneTieneFermiIPrimiEGliUltimiPerCentroComeNelRiferimento()
    {
        var result = new BoxDistribution().EqualGaps([
            new DistributedBox("a", 0, 10),
            new DistributedBox("wide", 20, 100),
            new DistributedBox("c", 50, 10),
        ]);

        var wide = result.Single(box => box.Id == "wide");
        Assert.Equal(20, wide.Start, 6);
        Assert.Equal(0, result.Single(box => box.Id == "a").Start, 6);
    }

    [Fact]
    public void LaDistribuzioneIgnoraMisureNonFinite()
    {
        var boxes = new[] { new DistributedBox("a", 0, 10), new DistributedBox("b", double.NaN, 5), new DistributedBox("c", 40, 10) };

        Assert.Same(boxes, new BoxDistribution().EqualGaps(boxes));
    }

    [Fact]
    public void UnViewBoxNonFinitoNonProduceCoordinateNaN()
    {
        var drawing = new PulseOffice.Drawing.Shapes.VectorOutlineReader().Read(
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 NaN 10' width='Infinity' height='5'><path d='M0 0 L4 4'/></svg>");

        var points = new PulseOffice.Drawing.Shapes.SegmentPoints().Of(drawing.Paths.Single().Outline);
        Assert.All(points, point => Assert.True(double.IsFinite(point.X) && double.IsFinite(point.Y)));
    }

    [Fact]
    public void RegolareSoloIlRossoNonScurisceGliAltriCanali()
    {
        var pixels = new byte[256 * 4];
        for (var value = 0; value < 256; value++)
        {
            pixels[value * 4] = (byte)value;
            pixels[value * 4 + 1] = (byte)value;
            pixels[value * 4 + 3] = 255;
        }

        new PulseOffice.Drawing.Assets.Filters.ColorAdjustmentFilter(new PulseOffice.Drawing.Assets.Filters.ColorAdjustment(Red: 10))
            .Apply(new PulseOffice.Drawing.Assets.Filters.BgraImage(pixels, 256, 1));

        for (var value = 0; value < 256; value++)
        {
            Assert.Equal(value, pixels[value * 4]);
            Assert.Equal(value, pixels[value * 4 + 1]);
        }
    }
}
