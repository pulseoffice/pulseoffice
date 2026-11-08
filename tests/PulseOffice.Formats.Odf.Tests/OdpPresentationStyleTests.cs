using System.IO.Compression;
using System.Text;
using PulseOffice.Presentations;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdpPresentationStyleTests
{
    private const string Namespaces =
        "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
        "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" " +
        "xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" " +
        "xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" office:version=\"1.3\"";

    private const string Styles =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-styles " + Namespaces + "><office:styles>" +
        "<style:style style:name=\"Default-title\" style:family=\"presentation\"><style:paragraph-properties fo:text-align=\"center\"/>" +
        "<style:text-properties fo:font-size=\"44pt\" fo:color=\"#c00000\" fo:font-weight=\"bold\"/></style:style>" +
        "<style:style style:name=\"Default-outline1\" style:family=\"presentation\"><style:text-properties fo:font-size=\"32pt\" fo:color=\"#003366\"/></style:style>" +
        "<style:style style:name=\"Default-outline2\" style:family=\"presentation\" style:parent-style-name=\"Default-outline1\"><style:text-properties fo:font-size=\"28pt\"/></style:style>" +
        "</office:styles><office:master-styles><style:master-page style:name=\"Default\">" +
        "<draw:frame draw:layer=\"backgroundobjects\" svg:x=\"2cm\" svg:y=\"2cm\" svg:width=\"4cm\" svg:height=\"3cm\"><draw:image xlink:href=\"Pictures/logo.svg\" draw:mime-type=\"image/svg+xml\"/><draw:image xlink:href=\"Pictures/logo.png\"/></draw:frame>" +
        "</style:master-page></office:master-styles></office:document-styles>";

    private static readonly byte[] Pixel = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");

    private const string Content =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><office:document-content " + Namespaces + "><office:automatic-styles>" +
        "<style:style style:name=\"pr1\" style:family=\"presentation\" style:parent-style-name=\"Default-title\"/>" +
        "<style:style style:name=\"pr2\" style:family=\"presentation\" style:parent-style-name=\"Default-outline1\"/>" +
        "<style:style style:name=\"P1\" style:family=\"paragraph\"><style:text-properties fo:font-size=\"20pt\"/></style:style>" +
        "</office:automatic-styles><office:body><office:presentation><draw:page draw:name=\"p1\" draw:master-page-name=\"Default\">" +
        "<draw:frame presentation:style-name=\"pr1\" presentation:class=\"title\" svg:x=\"1cm\" svg:y=\"1cm\" svg:width=\"20cm\" svg:height=\"3cm\">" +
        "<draw:text-box><text:p>Titolo</text:p></draw:text-box></draw:frame>" +
        "<draw:frame presentation:style-name=\"pr2\" presentation:class=\"outline\" svg:x=\"1cm\" svg:y=\"5cm\" svg:width=\"20cm\" svg:height=\"10cm\">" +
        "<draw:text-box><text:list><text:list-item><text:p>Uno</text:p><text:list><text:list-item><text:p>Due</text:p></text:list-item></text:list></text:list-item>" +
        "<text:list-item><text:p text:style-name=\"P1\">Tre</text:p></text:list-item></text:list></draw:text-box></draw:frame>" +
        "</draw:page></office:presentation></office:body></office:document-content>";

    private Presentation Read(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var result = new OdpReader(new ThemeCatalog(), new MasterFactory()).Read(stream);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private Presentation Read() => Read(Content);

    private Presentation Read(string content)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "mimetype", "application/vnd.oasis.opendocument.presentation");
            Add(archive, "styles.xml", Styles);
            Add(archive, "content.xml", content);
            using var picture = archive.CreateEntry("Pictures/logo.png", CompressionLevel.NoCompression).Open();
            picture.Write(Pixel);
            picture.Dispose();
            Add(archive, "Pictures/logo.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1 1\"><rect width=\"1\" height=\"1\"/></svg>");
        }
        buffer.Position = 0;
        var result = new OdpReader(new ThemeCatalog(), new MasterFactory()).Read(buffer);
        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private void Add(ZipArchive archive, string name, string text)
    {
        using var stream = archive.CreateEntry(name, CompressionLevel.NoCompression).Open();
        stream.Write(Encoding.UTF8.GetBytes(text));
    }

    [Fact]
    public void IlTitoloPrendeCorpoColoreGrassettoEAllineamentoDalloStileDiPresentazione()
    {
        var title = Read().Slides[0].Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Title).Text.Paragraphs[0];

        Assert.Equal(44, title.FontSize);
        Assert.Equal(0xFFC00000u, title.Color);
        Assert.True(title.Bold);
        Assert.Equal(TextAlign.Center, title.Align);
    }

    [Fact]
    public void IlivelliDelloSchemaUsanoGliStiliOutlineCorrispondentiEIlParagrafoPrevale()
    {
        var body = Read().Slides[0].Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Body).Text.Paragraphs;

        Assert.Equal(32, body[0].FontSize);
        Assert.Equal(28, body[1].FontSize);
        Assert.Equal(0xFF003366u, body[1].Color);
        Assert.Equal(20, body[2].FontSize);
    }

    [Fact]
    public void LeImmaginiDellaPaginaSchemaSiLeggonoESiRiscrivonoInOdp()
    {
        var deck = Read();
        var image = Assert.IsType<PulseOffice.Drawing.Assets.VectorGraphicObject>(Assert.IsType<ObjectElement>(Assert.Single(deck.Master.Objects)).Content);
        Assert.Contains("<rect", image.Asset.SvgContent, StringComparison.Ordinal);

        using var buffer = new MemoryStream();
        var written = new OdpWriter().Write(deck, buffer);
        Assert.True(written.Succeeded);
        Assert.DoesNotContain(written.Diagnostics, diagnostic => diagnostic.Code == "ODP020");
        Assert.DoesNotContain(new OdpWriter().CheckFidelity(deck).Losses, diagnostic => diagnostic.Code == "ODP020");

        var back = Read(buffer.ToArray());
        var again = Assert.IsType<ObjectElement>(Assert.Single(back.Master.Objects));
        Assert.Contains("<rect", Assert.IsType<PulseOffice.Drawing.Assets.VectorGraphicObject>(again.Content).Asset.SvgContent, StringComparison.Ordinal);
        Assert.Equal(deck.Master.Objects[0].Bounds, again.Bounds);
    }

    [Fact]
    public void UnaSequenzaDiSpaziSproporzionataVieneLimitata()
    {
        var presentation = Read(Content.Replace("<text:p>Titolo</text:p>", "<text:p>Ti<text:s text:c=\"2000000000\"/>tolo</text:p>", StringComparison.Ordinal));

        var title = presentation.Slides[0].Elements.OfType<TextElement>().Single(e => e.Text.Role == PlaceholderRole.Title);
        Assert.InRange(title.Text.Paragraphs[0].Text.Length, 7, 100_006);
    }
}
