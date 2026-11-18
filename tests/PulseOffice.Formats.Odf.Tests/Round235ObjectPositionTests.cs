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

public class Round235ObjectPositionTests
{
    private static (ObjectPlacement Read, string Content) RoundTrip(ObjectPlacement placement)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock("p1", "text", DefaultStyleNames.Standard));
        document.Objects.Add(new AnchoredObject("img", new ParagraphAnchor("p1"), new EmbeddedContent(new RasterImageObject { Id = "r", Data = new byte[] { 1 }, MediaType = "image/png" }),
            new DocumentSize(Twip.FromPoints(20), Twip.FromPoints(20))) { Placement = placement });
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        var bytes = stream.ToArray();
        string content;
        using (var zip = new ZipArchive(new MemoryStream(bytes)))
        using (var reader = new StreamReader(zip.GetEntry("content.xml")!.Open()))
            content = reader.ReadToEnd();
        return (Assert.Single(new OdtReader().Read(new MemoryStream(bytes)).Value!.Objects.All).Placement, content);
    }

    [Fact]
    public void The_top_margin_is_written_as_page_and_read_back_as_margin()
    {
        var (read, content) = RoundTrip(new ObjectPlacement(Twip.Zero, Twip.FromPoints(5)) { Vertical = VerticalRelation.Margin });

        Assert.Contains("style:vertical-rel=\"page\"", content, StringComparison.Ordinal);
        Assert.Equal(VerticalRelation.Margin, read.Vertical);
    }

    [Fact]
    public void The_paragraph_area_is_paragraph_content()
    {
        var (read, content) = RoundTrip(new ObjectPlacement(Twip.Zero, Twip.Zero) { Vertical = VerticalRelation.ParagraphArea });

        Assert.Contains("style:vertical-rel=\"paragraph-content\"", content, StringComparison.Ordinal);
        Assert.Equal(VerticalRelation.ParagraphArea, read.Vertical);
    }

    [Fact]
    public void Inside_and_outside_round_trip()
    {
        var (read, content) = RoundTrip(new ObjectPlacement(Twip.Zero, Twip.Zero) { Horizontal = HorizontalRelation.Page, HorizontalAlign = ObjectAlignment.Outside });

        Assert.Contains("style:horizontal-pos=\"outside\"", content, StringComparison.Ordinal);
        Assert.Equal(ObjectAlignment.Outside, read.HorizontalAlign);
    }
}
