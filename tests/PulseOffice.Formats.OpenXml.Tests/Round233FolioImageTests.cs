using System.IO.Compression;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round233FolioImageTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static TextDocument WithImage(RasterImageObject image)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock("p1", "text", DefaultStyleNames.Standard));
        document.Objects.Add(new AnchoredObject("img", new ParagraphAnchor("p1"), new EmbeddedContent(image),
            new DocumentSize(Twip.FromCentimeters(1), Twip.FromCentimeters(1))));
        return document;
    }

    private (TextDocument Read, byte[] Bytes) RoundTrip(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        var bytes = stream.ToArray();
        var read = _composer.Reader(DocxFlavour.Document).Read(new MemoryStream(bytes));
        Assert.True(read.Succeeded);
        return (read.Value!, bytes);
    }

    private static RasterImageObject Raster(AnchoredObject item) => (RasterImageObject)((EmbeddedContent)item.Content).Value;

    [Fact]
    public void A_linked_image_is_written_as_a_linked_blip_and_read_back_linked()
    {
        var document = WithImage(new RasterImageObject { Id = "r", Data = ReadOnlyMemory<byte>.Empty, MediaType = "image/png", LinkPath = "/pictures/logo.png", Crop = (0.1, 0, 0, 0) });

        var (read, bytes) = RoundTrip(document);

        using (var zip = new ZipArchive(new MemoryStream(bytes)))
            Assert.DoesNotContain(zip.Entries, entry => entry.FullName.StartsWith("word/media/", StringComparison.Ordinal));
        var image = Raster(Assert.Single(read.Objects.All));
        Assert.Equal("/pictures/logo.png", image.LinkPath);
        Assert.Equal(0.1, image.Crop.Left, 3);
    }

    [Fact]
    public void A_sepia_filter_is_declared_lost_in_docx()
    {
        var document = WithImage(new RasterImageObject { Id = "r", Data = new byte[] { 1 }, MediaType = "image/png", Filter = ImageFilter.Sepia });

        Assert.Contains(_composer.Writer(DocxFlavour.Document).CheckFidelity(document).Losses, loss => loss.Code == "DOCX046");
    }

    [Fact]
    public void An_aligned_position_is_written_as_wp_align_and_read_back()
    {
        var document = WithImage(new RasterImageObject { Id = "r", Data = new byte[] { 1 }, MediaType = "image/png" });
        document.Objects.All[0].Placement = new ObjectPlacement(Twip.Zero, Twip.Zero)
            { Horizontal = HorizontalRelation.Page, HorizontalAlign = ObjectAlignment.Center, VerticalAlign = ObjectAlignment.End, Vertical = VerticalRelation.Page };

        var (read, bytes) = RoundTrip(document);

        using (var zip = new ZipArchive(new MemoryStream(bytes)))
        using (var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open()))
            Assert.Contains("<wp:align>center</wp:align>", reader.ReadToEnd(), StringComparison.Ordinal);
        var placement = Assert.Single(read.Objects.All).Placement;
        Assert.Equal(ObjectAlignment.Center, placement.HorizontalAlign);
        Assert.Equal(ObjectAlignment.End, placement.VerticalAlign);
    }
}
