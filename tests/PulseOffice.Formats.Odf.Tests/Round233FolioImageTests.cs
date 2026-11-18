using System.IO.Compression;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public class Round233FolioImageTests
{
    private static TextDocument WithImage(RasterImageObject image, DocumentSize size)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock("p1", "text", DefaultStyleNames.Standard));
        document.Objects.Add(new AnchoredObject("img", new ParagraphAnchor("p1"), new EmbeddedContent(image), size));
        return document;
    }

    private static (TextDocument Read, string Content) RoundTrip(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        var bytes = stream.ToArray();
        string content;
        using (var zip = new ZipArchive(new MemoryStream(bytes)))
        using (var reader = new StreamReader(zip.GetEntry("content.xml")!.Open()))
            content = reader.ReadToEnd();
        return (new OdtReader().Read(new MemoryStream(bytes)).Value!, content);
    }

    private static RasterImageObject Raster(AnchoredObject item) => (RasterImageObject)((EmbeddedContent)item.Content).Value;

    [Fact]
    public void The_clip_of_a_cropped_image_is_measured_on_the_original_image_not_on_the_frame()
    {
        var image = new RasterImageObject { Id = "r", Data = new byte[] { 1, 2, 3 }, MediaType = "image/png", PixelWidth = 100, PixelHeight = 40, Crop = (0.1, 0, 0.2, 0.25) };
        var document = WithImage(image, new DocumentSize(Twip.FromCentimeters(10), Twip.FromCentimeters(3)));

        var (read, content) = RoundTrip(document);

        Assert.Contains("rect(0pt, 15pt, 7.5pt, 7.5pt)", content, StringComparison.Ordinal);
        var crop = Raster(Assert.Single(read.Objects.All)).Crop;
        Assert.Equal(0.1, crop.Left, 3);
        Assert.Equal(0.2, crop.Right, 3);
        Assert.Equal(0.25, crop.Bottom, 3);
    }

    [Fact]
    public void Without_pixel_dimensions_the_clip_assumes_an_unscaled_image_and_round_trips()
    {
        var image = new RasterImageObject { Id = "r", Data = new byte[] { 1, 2, 3 }, MediaType = "image/png", Crop = (0.2, 0, 0.2, 0) };
        var document = WithImage(image, new DocumentSize(Twip.FromPoints(60), Twip.FromPoints(30)));

        var (read, content) = RoundTrip(document);

        Assert.Contains("rect(0pt, 20pt, 0pt, 20pt)", content, StringComparison.Ordinal);
        Assert.Equal(0.2, Raster(Assert.Single(read.Objects.All)).Crop.Left, 3);
    }

    [Fact]
    public void An_aligned_position_is_written_as_style_horizontal_pos_and_read_back()
    {
        var document = WithImage(new RasterImageObject { Id = "r", Data = new byte[] { 1 }, MediaType = "image/png" }, new DocumentSize(Twip.FromPoints(20), Twip.FromPoints(20)));
        document.Objects.All[0].Placement = new ObjectPlacement(Twip.Zero, Twip.Zero) { HorizontalAlign = ObjectAlignment.Center, VerticalAlign = ObjectAlignment.Start };

        var (read, content) = RoundTrip(document);

        Assert.Contains("style:horizontal-pos=\"center\"", content, StringComparison.Ordinal);
        var placement = Assert.Single(read.Objects.All).Placement;
        Assert.Equal(ObjectAlignment.Center, placement.HorizontalAlign);
        Assert.Equal(ObjectAlignment.Start, placement.VerticalAlign);
    }

    [Fact]
    public void A_rotated_image_is_written_with_a_standard_draw_transform_and_read_back_in_place()
    {
        var document = WithImage(new RasterImageObject { Id = "r", Data = new byte[] { 1 }, MediaType = "image/png" }, new DocumentSize(Twip.FromPoints(40), Twip.FromPoints(20)));
        document.Objects.All[0].RotationDegrees = 90;
        document.Objects.All[0].Placement = new ObjectPlacement(Twip.FromPoints(10), Twip.FromPoints(20));

        var (read, content) = RoundTrip(document);

        Assert.Contains("draw:transform=\"rotate (", content, StringComparison.Ordinal);
        var item = Assert.Single(read.Objects.All);
        Assert.Equal(90, item.RotationDegrees, 3);
        Assert.Equal(10, item.Placement.X.ToPoints(), 0);
        Assert.Equal(20, item.Placement.Y.ToPoints(), 0);
    }
}
