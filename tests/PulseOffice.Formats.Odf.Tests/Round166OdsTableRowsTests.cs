using System.IO.Compression;
using PulseOffice.Sheets.Addressing;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class Round166OdsTableRowsTests
{
    private const string Content =
        "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" "
        + "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.3\">"
        + "<office:body><office:spreadsheet><table:table table:name=\"S\">"
        + "<table:table-columns><table:table-column/><table:table-column/></table:table-columns>"
        + "<table:table-header-rows><table:table-row><table:table-cell office:value-type=\"float\" office:value=\"1\"/></table:table-row></table:table-header-rows>"
        + "<table:table-rows><table:table-row><table:table-cell office:value-type=\"float\" office:value=\"2\"/></table:table-row>"
        + "<table:table-row><table:table-cell/><table:table-cell office:value-type=\"float\" office:value=\"3\"/></table:table-row></table:table-rows>"
        + "</table:table></office:spreadsheet></office:body></office:document-content>";

    [Fact]
    public void Rows_inside_a_table_rows_wrapper_are_read_at_their_place()
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var mimetype = new StreamWriter(archive.CreateEntry("mimetype").Open())) mimetype.Write("application/vnd.oasis.opendocument.spreadsheet");
            using var writer = new StreamWriter(archive.CreateEntry("content.xml").Open());
            writer.Write(Content);
        }

        stream.Position = 0;
        var result = new OdsReader().Read(stream);

        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics));
        var sheet = result.Value!.Sheets[0];
        Assert.Equal(1.0, sheet.GetValue(new CellAddress(0, 0)).AsNumber());
        Assert.Equal(2.0, sheet.GetValue(new CellAddress(1, 0)).AsNumber());
        Assert.Equal(3.0, sheet.GetValue(new CellAddress(2, 1)).AsNumber());
    }
}
