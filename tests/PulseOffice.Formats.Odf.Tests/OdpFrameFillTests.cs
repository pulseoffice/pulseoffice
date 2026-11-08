using System.IO.Compression;
using System.Text;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpFrameFillTests
{
    private const string Namespaces =
        "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
        "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
        "xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" " +
        "xmlns:xlink=\"http://www.w3.org/1999/xlink\" office:version=\"1.3\"";

    private Presentation Read()
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, string text)
            {
                using var entry = archive.CreateEntry(name).Open();
                entry.Write(Encoding.UTF8.GetBytes(text));
            }
            Add("mimetype", "application/vnd.oasis.opendocument.presentation");
            Add("styles.xml", "<?xml version=\"1.0\"?><office:document-styles " + Namespaces + "><office:styles>" +
                "<draw:fill-image draw:name=\"Tile\" xlink:href=\"Pictures/t.png\" xlink:type=\"simple\" xlink:show=\"embed\" xlink:actuate=\"onLoad\"/></office:styles></office:document-styles>");
            Add("content.xml", "<?xml version=\"1.0\"?><office:document-content " + Namespaces + "><office:automatic-styles>" +
                "<style:style style:name=\"pr1\" style:family=\"presentation\"><style:graphic-properties draw:fill=\"solid\" draw:fill-color=\"#00a0e8\"/></style:style>" +
                "<style:style style:name=\"gr1\" style:family=\"graphic\"><style:graphic-properties draw:fill=\"bitmap\" draw:fill-image-name=\"Tile\" draw:fill-image-width=\"50%\" draw:fill-image-height=\"25%\" style:repeat=\"repeat\"/></style:style>" +
                "</office:automatic-styles><office:body><office:presentation><draw:page draw:name=\"p\">" +
                "<draw:frame presentation:style-name=\"pr1\" presentation:class=\"title\" svg:x=\"1cm\" svg:y=\"1cm\" svg:width=\"10cm\" svg:height=\"3cm\"><draw:text-box><text:p>Titolo</text:p></draw:text-box></draw:frame>" +
                "<draw:custom-shape draw:style-name=\"gr1\" svg:x=\"1cm\" svg:y=\"6cm\" svg:width=\"8cm\" svg:height=\"4cm\"><draw:enhanced-geometry draw:type=\"rectangle\"/></draw:custom-shape>" +
                "</draw:page></office:presentation></office:body></office:document-content>");
            using var picture = archive.CreateEntry("Pictures/t.png").Open();
            picture.Write(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg=="));
        }
        buffer.Position = 0;
        return new OdpReader(new ThemeCatalog(), new MasterFactory()).Read(buffer).Value!;
    }

    [Fact]
    public void Lo_sfondo_del_segnaposto_viene_dallo_stile_di_presentazione()
    {
        var title = Read().Slides[0].Elements.OfType<TextElement>().Single();
        Assert.Equal("#00A0E8", title.Backdrop!.Fill.ColorHex, ignoreCase: true);
    }

    [Fact]
    public void Le_tessere_in_percentuale_restano_relative_alla_forma_e_attraversano_ODP()
    {
        var deck = Read();
        ShapeBitmap Tile(Presentation presentation) =>
            presentation.Slides[0].Elements.OfType<ObjectElement>().Select(element => element.Content).OfType<ShapeObject>().Single().Style.Fill.Bitmap!;

        Assert.Equal((0.5, 0.25), (Tile(deck).TileWidthRatio, Tile(deck).TileHeightRatio));
        using var again = new MemoryStream();
        Assert.True(new OdpWriter().Write(deck, again).Succeeded);
        again.Position = 0;
        var back = new OdpReader(new ThemeCatalog(), new MasterFactory()).Read(again).Value!;
        Assert.Equal((0.5, 0.25), (Tile(back).TileWidthRatio, Tile(back).TileHeightRatio));
    }
}
