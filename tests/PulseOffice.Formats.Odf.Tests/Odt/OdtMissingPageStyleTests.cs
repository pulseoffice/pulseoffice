using System.IO.Compression;
using System.Text;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtMissingPageStyleTests
{
    private const string Namespaces =
        "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" " +
        "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.3\"";

    [Fact]
    public void UnTestoSenzaPagineSchemaHaComunqueLoStileDiPaginaPredefinito()
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(archive, "mimetype", "application/vnd.oasis.opendocument.text");
            Add(archive, "styles.xml", "<?xml version=\"1.0\"?><office:document-styles " + Namespaces + "><office:master-styles/></office:document-styles>");
            Add(archive, "content.xml", "<?xml version=\"1.0\"?><office:document-content " + Namespaces + "><office:body><office:text><text:p>Ciao</text:p></office:text></office:body></office:document-content>");
        }
        buffer.Position = 0;

        var document = new OdtReader().Read(buffer).Value!;

        Assert.NotEmpty(document.Styles.Page.All);
        Assert.NotNull(document.Styles.Page.Find(document.InitialPageStyle));
    }

    private void Add(ZipArchive archive, string name, string text)
    {
        using var stream = archive.CreateEntry(name, CompressionLevel.NoCompression).Open();
        stream.Write(Encoding.UTF8.GetBytes(text));
    }
}
