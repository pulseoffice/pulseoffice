using System.IO.Compression;
using PulseOffice.Formats.Odf;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdsRound34GroupPictureTests
{
    [Fact]
    public void A_grouped_svg_named_without_extension_is_not_read_as_a_raster_and_its_loss_is_declared()
    {
        var read = new OdsReader().Read(Ods(
            "<table:shapes><draw:g><draw:rect svg:width=\"2cm\" svg:height=\"2cm\" svg:x=\"1cm\" svg:y=\"1cm\"/>"
            + "<draw:frame svg:width=\"2cm\" svg:height=\"2cm\" svg:x=\"4cm\" svg:y=\"1cm\"><draw:image xlink:href=\"Pictures/vector\" draw:mime-type=\"image/svg+xml\"/></draw:frame></draw:g></table:shapes>"
            + "<table:table-row><table:table-cell/></table:table-row>"));

        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "ODF193");
        var group = Assert.IsType<PulseOffice.Drawing.Shapes.ShapeGroupObject>(Assert.Single(read.Value!.Sheets[0].Objects).Content);
        Assert.DoesNotContain(group.Children, child => child.Content is PulseOffice.Drawing.Assets.RasterImageObject);
    }

    private static MemoryStream Ods(string rows)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var mimetype = new StreamWriter(archive.CreateEntry("mimetype").Open())) mimetype.Write("application/vnd.oasis.opendocument.spreadsheet");
            using (var vector = new StreamWriter(archive.CreateEntry("Pictures/vector").Open()))
                vector.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\"/></svg>");
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
