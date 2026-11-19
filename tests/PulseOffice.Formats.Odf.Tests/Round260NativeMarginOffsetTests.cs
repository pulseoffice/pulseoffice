using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Assets;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round260NativeMarginOffsetTests
{
    private static readonly XNamespace Svg = "urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0";
    private static readonly XNamespace Draw = "urn:oasis:names:tc:opendocument:xmlns:drawing:1.0";

    [Theory]
    [InlineData(VerticalRelation.BottomMargin)]
    [InlineData(VerticalRelation.OutsideMargin)]
    public void A_bottom_margin_offset_is_written_from_the_page_top_and_read_back_exactly(VerticalRelation relation)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var page = document.Styles.Page.Find(document.InitialPageStyle)!;
        document.Body.Add(new ParagraphBlock("p1", "text", DefaultStyleNames.Standard));
        var placement = new ObjectPlacement(Twip.FromPoints(3), Twip.FromPoints(5)) { Vertical = relation };
        document.Objects.Add(new AnchoredObject("img", new ParagraphAnchor("p1"), new EmbeddedContent(new RasterImageObject { Id = "r", Data = new byte[] { 1 }, MediaType = "image/png" }),
            new DocumentSize(Twip.FromPoints(20), Twip.FromPoints(20))) { Placement = placement });
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);

        stream.Position = 0;
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        {
            using var content = archive.GetEntry("content.xml")!.Open();
            var y = (string)XDocument.Load(content).Descendants(Draw + "frame").Single().Attribute(Svg + "y")!;
            var expected = (page.Size.Height - page.Margins.Bottom + Twip.FromPoints(5)).ToCentimeters();
            Assert.Equal(expected, Centimeters(y), 2);
        }
        stream.Position = 0;
        var read = Assert.Single(new OdtReader().Read(stream).Value!.Objects.All);

        Assert.Equal(placement, read.Placement);
    }

    private static double Centimeters(string length) =>
        length.EndsWith("pt", StringComparison.Ordinal)
            ? double.Parse(length[..^2], CultureInfo.InvariantCulture) * 2.54 / 72
            : double.Parse(length.Replace("cm", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture);
}
