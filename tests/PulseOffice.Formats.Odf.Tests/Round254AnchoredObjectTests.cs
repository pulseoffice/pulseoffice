using System.IO.Compression;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round254AnchoredObjectTests
{
    private static (AnchoredObject Read, string Content) RoundTrip(Action<AnchoredObject> shape)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock("p1", "text", DefaultStyleNames.Standard));
        var item = new AnchoredObject("img", new ParagraphAnchor("p1"), new EmbeddedContent(new RasterImageObject { Id = "r", Data = new byte[] { 1 }, MediaType = "image/png" }),
            new DocumentSize(Twip.FromPoints(20), Twip.FromPoints(20)));
        shape(item);
        document.Objects.Add(item);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        var bytes = stream.ToArray();
        string content;
        using (var zip = new ZipArchive(new MemoryStream(bytes)))
        using (var reader = new StreamReader(zip.GetEntry("content.xml")!.Open()))
            content = reader.ReadToEnd();
        return (Assert.Single(new OdtReader().Read(new MemoryStream(bytes)).Value!.Objects.All), content);
    }

    [Fact]
    public void Not_laying_out_in_cell_is_flow_with_text_false_and_overlap_is_kept()
    {
        var (read, content) = RoundTrip(item =>
        {
            item.LayoutInCell = false;
            item.AllowOverlap = false;
        });

        Assert.Contains("style:flow-with-text=\"false\"", content, StringComparison.Ordinal);
        Assert.False(read.LayoutInCell);
        Assert.False(read.AllowOverlap);
    }

    [Fact]
    public void Inside_and_outside_margin_relations_round_trip_over_their_nearest_native_relation()
    {
        var (read, content) = RoundTrip(item => item.Placement = new ObjectPlacement(Twip.Zero, Twip.Zero)
        {
            Horizontal = HorizontalRelation.OutsideMargin, Vertical = VerticalRelation.InsideMargin,
        });

        Assert.Contains("style:horizontal-rel=\"page-end-margin\"", content, StringComparison.Ordinal);
        Assert.Equal(HorizontalRelation.OutsideMargin, read.Placement.Horizontal);
        Assert.Equal(VerticalRelation.InsideMargin, read.Placement.Vertical);
    }
}
