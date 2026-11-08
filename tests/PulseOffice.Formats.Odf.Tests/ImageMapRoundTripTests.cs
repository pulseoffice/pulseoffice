using PulseOffice.Document.Embedding;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using PulseOffice.Presentations;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class ImageMapRoundTripTests
{
    private static ImageMap Map()
    {
        var map = new ImageMap();
        map.Replace(
        [
            new ImageMapArea { Id = "a", Shape = ImageMapShape.Rectangle, Points = [(0.1, 0.2), (0.4, 0.6)], Target = "https://example.org/nord", Description = "Nord" },
            new ImageMapArea { Id = "b", Shape = ImageMapShape.Circle, Points = [(0.7, 0.5), (0.8, 0.5)], Target = "https://example.org/sud" },
            new ImageMapArea { Id = "c", Shape = ImageMapShape.Polygon, Points = [(0.1, 0.9), (0.3, 0.7), (0.5, 0.9)], Target = "#slide2" },
        ]);
        return map;
    }

    private static void AssertSame(ImageMap map)
    {
        Assert.Equal(3, map.Areas.Count);
        var rectangle = map.Areas.Single(area => area.Shape == ImageMapShape.Rectangle);
        Assert.Equal(0.1, rectangle.Points[0].X, 3);
        Assert.Equal(0.6, rectangle.Points[1].Y, 3);
        Assert.Equal("https://example.org/nord", rectangle.Target);
        Assert.Equal("Nord", rectangle.Description);
        Assert.True(map.Areas.Single(area => area.Shape == ImageMapShape.Circle).Contains(0.75, 0.5));
        var polygon = map.Areas.Single(area => area.Shape == ImageMapShape.Polygon);
        Assert.Equal(3, polygon.Points.Count);
        Assert.Equal(0.3, polygon.Points[1].X, 3);
        Assert.Equal("#slide2", polygon.Target);
    }

    [Fact]
    public void LaMappaImmagineSopravviveInOds()
    {
        var workbook = Workbook.CreateDefault();
        workbook.Sheets[0].Objects.Add(new PlacedObject
        {
            Id = "img",
            Anchor = new CellAddress(1, 1),
            Size = new DocumentSize(Twip.FromCentimeters(8), Twip.FromCentimeters(5)),
            Content = new RasterImageObject { Id = "img", Data = new byte[] { 1, 2, 3 }, MediaType = "image/png", Map = Map() },
        });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;

        var image = new OdsReader().Read(stream).Value!.Sheets[0].Objects.Select(o => o.Content).OfType<RasterImageObject>().Single();
        AssertSame(image.Map);
    }

    [Fact]
    public void LaMappaImmagineSopravviveInOdp()
    {
        var deck = new Presentation();
        var slide = new SlideFactory().Create("s1", deck.Master, SlideLayout.Blank);
        deck.AddExisting(slide);
        slide.Add(new ObjectElement
        {
            Id = "img",
            Bounds = new DocumentRect(new Twip(1000), new Twip(1000), Twip.FromCentimeters(8), Twip.FromCentimeters(5)),
            Content = new RasterImageObject { Id = "img", Data = new byte[] { 1, 2, 3 }, MediaType = "image/png", Map = Map() },
        });

        using var stream = new MemoryStream();
        new OdpWriter().Write(deck, stream);
        stream.Position = 0;

        var back = new OdpReader(new ThemeCatalog(), new MasterFactory()).Read(stream).Value!;
        var image = back.Slides[0].Elements.OfType<ObjectElement>().Select(o => o.Content).OfType<RasterImageObject>().Single();
        AssertSame(image.Map);
    }
}
