using System.IO.Compression;
using System.Text;
using PulseOffice.Formats.Odf;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Presentations;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdfDeepGroupTests
{
    private const int Depth = 50_000;

    private const string Namespaces = "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" "
        + "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" "
        + "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\"";

    [Fact]
    public void Gruppi_annidati_a_dismisura_in_una_presentazione_non_fermano_il_processo()
    {
        var page = "<draw:page draw:name=\"p1\">" + Nested() + "</draw:page>";
        var read = new OdpReader(new ThemeCatalog(), new MasterFactory()).Read(Package("application/vnd.oasis.opendocument.presentation",
            $"<office:document-content {Namespaces} office:version=\"1.3\"><office:body><office:presentation>{page}</office:presentation></office:body></office:document-content>"));
        Assert.NotNull(read);
    }

    [Fact]
    public void Gruppi_annidati_a_dismisura_in_un_testo_non_fermano_il_processo()
    {
        var read = new OdtReader().Read(Package("application/vnd.oasis.opendocument.text",
            $"<office:document-content {Namespaces} office:version=\"1.3\"><office:body><office:text><text:p>{Nested()}</text:p></office:text></office:body></office:document-content>"));
        Assert.NotNull(read);
    }

    private static string Nested()
    {
        var builder = new StringBuilder();
        for (var level = 0; level < Depth; level++) builder.Append("<draw:g>");
        builder.Append("<draw:rect svg:x=\"0cm\" svg:y=\"0cm\" svg:width=\"1cm\" svg:height=\"1cm\"/>");
        for (var level = 0; level < Depth; level++) builder.Append("</draw:g>");
        return builder.ToString();
    }

    private static MemoryStream Package(string mimeType, string content)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var mimetype = new StreamWriter(archive.CreateEntry("mimetype").Open())) mimetype.Write(mimeType);
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
            writer.Write(content);
        }

        stream.Position = 0;
        return stream;
    }
}
