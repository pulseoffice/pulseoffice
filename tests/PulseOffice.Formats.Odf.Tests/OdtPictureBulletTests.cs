using System.IO.Compression;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdtPictureBulletTests
{
    [Theory]
    [InlineData("<text:list-level-style-image text:level=\"1\" xlink:href=\"Pictures/punto.png\"/>", true)]
    [InlineData("<text:list-level-style-bullet text:level=\"1\" text:bullet-char=\"-\"/>", false)]
    public void Un_punto_elenco_a_immagine_si_dichiara(string level, bool declared)
    {
        var read = new OdtReader().Read(Odt($"<text:list-style style:name=\"L1\">{level}</text:list-style>"));
        Assert.NotNull(read.Value);
        Assert.Equal(declared, read.Diagnostics.Any(diagnostic => diagnostic.Code == "ODF178"));
    }

    private static MemoryStream Odt(string listStyle)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var mimetype = new StreamWriter(archive.CreateEntry("mimetype").Open())) mimetype.Write("application/vnd.oasis.opendocument.text");
            using var content = new StreamWriter(archive.CreateEntry("content.xml").Open());
            content.Write("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" "
                + "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" office:version=\"1.3\"><office:automatic-styles>" + listStyle
                + "</office:automatic-styles><office:body><office:text><text:list text:style-name=\"L1\"><text:list-item><text:p>voce</text:p></text:list-item></text:list></office:text></office:body></office:document-content>");
        }

        stream.Position = 0;
        return stream;
    }
}
