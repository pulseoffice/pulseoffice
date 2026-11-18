using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public class Round235ObjectPositionTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private ObjectPlacement RoundTrip(ObjectPlacement placement)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock("p1", "text", DefaultStyleNames.Standard));
        document.Objects.Add(new AnchoredObject("img", new ParagraphAnchor("p1"), new EmbeddedContent(new RasterImageObject { Id = "r", Data = new byte[] { 1 }, MediaType = "image/png" }),
            new DocumentSize(Twip.FromCentimeters(1), Twip.FromCentimeters(1))) { Placement = placement });
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        var read = _composer.Reader(DocxFlavour.Document).Read(new MemoryStream(stream.ToArray()));
        return Assert.Single(read.Value!.Objects.All).Placement;
    }

    [Fact]
    public void The_column_relation_is_read_back_as_column()
    {
        Assert.Equal(HorizontalRelation.Column, RoundTrip(new ObjectPlacement(Twip.Zero, Twip.Zero) { Horizontal = HorizontalRelation.Column }).Horizontal);
    }

    [Fact]
    public void The_top_margin_and_mirrored_alignments_round_trip()
    {
        var read = RoundTrip(new ObjectPlacement(Twip.Zero, Twip.Zero)
        {
            Horizontal = HorizontalRelation.Page,
            HorizontalAlign = ObjectAlignment.Inside,
            Vertical = VerticalRelation.Margin,
            VerticalAlign = ObjectAlignment.Outside,
        });

        Assert.Equal(VerticalRelation.Margin, read.Vertical);
        Assert.Equal(ObjectAlignment.Inside, read.HorizontalAlign);
        Assert.Equal(ObjectAlignment.Outside, read.VerticalAlign);
    }
}
