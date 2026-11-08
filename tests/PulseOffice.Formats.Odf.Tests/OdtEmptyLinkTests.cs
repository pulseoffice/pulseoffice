using System.IO.Compression;
using PulseOffice.Documents.Blocks;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdtEmptyLinkTests
{
    [Theory]
    [InlineData("<text:a xlink:type=\"simple\" xlink:href=\"https://example.org/\"/>")]
    [InlineData("<text:a xlink:type=\"simple\">testo</text:a>")]
    public void Un_collegamento_vuoto_o_senza_destinazione_si_dichiara(string link)
    {
        var read = new OdtReader().Read(Odt($"<text:p>prima {link} dopo</text:p>"));
        Assert.NotNull(read.Value);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "ODF175");
    }

    [Fact]
    public void Un_collegamento_con_testo_e_destinazione_non_si_dichiara()
    {
        var read = new OdtReader().Read(Odt("<text:p><text:a xlink:type=\"simple\" xlink:href=\"https://example.org/\">sito</text:a></text:p>"));
        var paragraph = Assert.IsType<ParagraphBlock>(read.Value!.Body.Blocks.First());
        Assert.Equal("https://example.org/", paragraph.Content.Runs.Single(run => run.Text == "sito").Hyperlink);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODF175");
    }

    [Theory]
    [InlineData("<draw:frame draw:name=\"F\" text:anchor-type=\"paragraph\" svg:width=\"2cm\" svg:height=\"2cm\"><draw:image xlink:href=\"Pictures/assente.png\"/></draw:frame>", "ODF176")]
    [InlineData("<draw:frame draw:name=\"V\" text:anchor-type=\"paragraph\" svg:width=\"2cm\" svg:height=\"2cm\"><draw:image xlink:href=\"\"><text:p/></draw:image></draw:frame>", "ODF176")]
    [InlineData("<draw:g><draw:frame draw:name=\"G\" svg:width=\"2cm\" svg:height=\"2cm\"><draw:image xlink:href=\"\"/></draw:frame></draw:g>", "ODF176")]
    [InlineData("<draw:frame draw:name=\"O\" text:anchor-type=\"paragraph\" svg:width=\"2cm\" svg:height=\"2cm\"><draw:object-ole xlink:href=\"./Object 1\"/></draw:frame>", "ODF177")]
    public void Una_cornice_senza_dati_si_dichiara(string frame, string code)
    {
        var read = new OdtReader().Read(Odt($"<text:p>testo{frame}</text:p>"));
        Assert.NotNull(read.Value);
        Assert.Empty(read.Value!.Objects.All);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    private static MemoryStream Odt(string body)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var mimetype = new StreamWriter(archive.CreateEntry("mimetype").Open())) mimetype.Write("application/vnd.oasis.opendocument.text");
            using var content = new StreamWriter(archive.CreateEntry("content.xml").Open());
            content.Write("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" "
                + "xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" office:version=\"1.3\"><office:body><office:text>" + body + "</office:text></office:body></office:document-content>");
        }

        stream.Position = 0;
        return stream;
    }
}
