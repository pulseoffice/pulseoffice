using System.IO.Compression;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsThirteenthAuditTests
{
    [Fact]
    public void Le_unioni_sovrapposte_si_dichiarano()
    {
        var read = new OdsReader().Read(Ods(
            "<table:table-row><table:table-cell/><table:table-cell table:number-rows-spanned=\"3\"/></table:table-row>"
            + "<table:table-row><table:table-cell table:number-columns-spanned=\"2\"/><table:covered-table-cell/></table:table-row>"));

        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "ODF191");
        Assert.Single(read.Value!.Sheets[0].Merges.All);
    }

    [Fact]
    public void Le_unioni_separate_non_si_dichiarano()
    {
        var read = new OdsReader().Read(Ods(
            "<table:table-row><table:table-cell table:number-columns-spanned=\"2\"/><table:covered-table-cell/></table:table-row>"
            + "<table:table-row><table:table-cell table:number-columns-spanned=\"2\"/><table:covered-table-cell/></table:table-row>"));

        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODF191");
        Assert.Equal(2, read.Value!.Sheets[0].Merges.Count);
    }

    [Fact]
    public void Le_immagini_collegate_si_conservano_e_quelle_mancanti_si_dichiarano()
    {
        var read = new OdsReader().Read(Ods(
            "<table:shapes><draw:frame draw:name=\"Immagine\" svg:width=\"2cm\" svg:height=\"2cm\" svg:x=\"1cm\" svg:y=\"1cm\"><draw:image xlink:href=\"../collegata.jpg\"/></draw:frame></table:shapes>"
            + "<table:table-row><table:table-cell/></table:table-row>"));

        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODF192");
        var image = Assert.IsType<PulseOffice.Drawing.Assets.RasterImageObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
        Assert.Equal("collegata.jpg", image.LinkPath);

        var missing = new OdsReader().Read(Ods(
            "<table:shapes><draw:frame draw:name=\"Immagine\" svg:width=\"2cm\" svg:height=\"2cm\" svg:x=\"1cm\" svg:y=\"1cm\"><draw:image xlink:href=\"Pictures/assente.png\"/></draw:frame></table:shapes>"
            + "<table:table-row><table:table-cell/></table:table-row>"));
        Assert.Contains(missing.Diagnostics, diagnostic => diagnostic.Code == "ODF192");
    }

    [Fact]
    public void I_grafici_nei_gruppi_si_dichiarano_e_le_immagini_collegate_si_leggono()
    {
        var read = new OdsReader().Read(Ods(
            "<table:shapes><draw:g><draw:rect svg:width=\"2cm\" svg:height=\"2cm\" svg:x=\"1cm\" svg:y=\"1cm\"/>"
            + "<draw:frame svg:width=\"2cm\" svg:height=\"2cm\" svg:x=\"4cm\" svg:y=\"1cm\"><draw:image xlink:href=\"../rosa.png\"/></draw:frame>"
            + "<draw:frame svg:width=\"2cm\" svg:height=\"2cm\" svg:x=\"7cm\" svg:y=\"1cm\"><draw:object xlink:href=\"./Object 1\"/></draw:frame></draw:g></table:shapes>"
            + "<table:table-row><table:table-cell/></table:table-row>"));

        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "ODF193");
        var group = Assert.IsType<PulseOffice.Drawing.Shapes.ShapeGroupObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
        Assert.Contains(group.Children, child => child.Content is PulseOffice.Drawing.Assets.RasterImageObject { LinkPath: "rosa.png" });
    }

    [Fact]
    public void I_collegamenti_sugli_oggetti_si_leggono()
    {
        var read = new OdsReader().Read(Ods(
            "<table:shapes><draw:a xlink:href=\"https://example.org/\"><draw:rect draw:name=\"Casella\" svg:width=\"2cm\" svg:height=\"2cm\" svg:x=\"1cm\" svg:y=\"1cm\"/></draw:a></table:shapes>"
            + "<table:table-row><table:table-cell/></table:table-row>"));

        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODF194");
        Assert.Contains(read.Value!.Sheets[0].Objects, placed => placed.Hyperlink == "https://example.org/");
    }

    private static MemoryStream Ods(string rows)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var mimetype = new StreamWriter(archive.CreateEntry("mimetype").Open())) mimetype.Write("application/vnd.oasis.opendocument.spreadsheet");
            using var content = new StreamWriter(archive.CreateEntry("content.xml").Open());
            content.Write("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" "
                + "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" "
                + "xmlns:xlink=\"http://www.w3.org/1999/xlink\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" office:version=\"1.3\"><office:body><office:spreadsheet><table:table table:name=\"Foglio1\">"
                + rows + "</table:table></office:spreadsheet></office:body></office:document-content>");
        }

        stream.Position = 0;
        return stream;
    }
}
