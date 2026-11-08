using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using PulseOffice.Sheets;
using PulseOffice.Sheets.Addressing;
using PulseOffice.Sheets.Values;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class StreamingAuditTests
{
    private const string Head =
        "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" " +
        "xmlns:table=\"urn:oasis:names:tc:opendocument:xmlns:table:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" " +
        "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" office:version=\"1.3\">" +
        "<office:body><office:spreadsheet>";

    private const string Tail = "</office:spreadsheet></office:body></office:document-content>";

    private static Workbook Read(string body)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var mimetype = zip.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var writer = new StreamWriter(mimetype.Open(), new UTF8Encoding(false)))
                writer.Write("application/vnd.oasis.opendocument.spreadsheet");
            using (var writer = new StreamWriter(zip.CreateEntry("content.xml").Open(), new UTF8Encoding(false)))
                writer.Write(Head + body + Tail);
        }
        stream.Position = 0;
        return new OdsReader().Read(stream).Require();
    }

    [Fact]
    public void UnaTabellaDentroUnaCornicePrimaDelleRigheNonDiventaUnFoglio()
    {
        var workbook = Read(
            "<table:table table:name=\"Primo\"><table:shapes><draw:frame svg:width=\"1cm\" svg:height=\"1cm\" svg:x=\"0cm\" svg:y=\"0cm\"><draw:text-box>" +
            "<table:table table:name=\"Annidata\"><table:table-row><table:table-cell office:value-type=\"float\" office:value=\"9\"/></table:table-row></table:table>" +
            "</draw:text-box></draw:frame></table:shapes>" +
            "<table:table-column/><table:table-row><table:table-cell office:value-type=\"float\" office:value=\"1\"/></table:table-row>" +
            "<table:table-row><table:table-cell office:value-type=\"float\" office:value=\"2\"/></table:table-row></table:table>" +
            "<table:table table:name=\"Secondo\"><table:table-column/><table:table-row><table:table-cell office:value-type=\"float\" office:value=\"3\"/></table:table-row></table:table>");

        Assert.Equal(["Primo", "Secondo"], workbook.Sheets.Select(sheet => sheet.Name));
        Assert.Equal(2d, workbook.Sheets[0].GetValue(new CellAddress(1, 0)).AsNumber());
        Assert.Equal(3d, workbook.Sheets[1].GetValue(new CellAddress(0, 0)).AsNumber());
    }

    [Fact]
    public void GruppiSovrappostiSiScrivonoBenAnnidati()
    {
        var workbook = Workbook.CreateDefault();
        var sheet = workbook.Sheets[0];
        for (var row = 0; row < 12; row++) sheet.SetValue(new CellAddress(row, 0), CellValue.Number(row));
        sheet.RowGroups.Add(new OutlineGroup { First = 0, Last = 5 });
        sheet.RowGroups.Add(new OutlineGroup { First = 3, Last = 8 });

        using var stream = new MemoryStream();
        new OdsWriter().Write(workbook, stream);
        stream.Position = 0;
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true))
        using (var content = zip.GetEntry("content.xml")!.Open())
        {
            var groups = XDocument.Load(content).Descendants().Where(element => element.Name.LocalName == "table-row-group").ToList();
            Assert.Equal(2, groups.Count);
            Assert.DoesNotContain(groups[0].Descendants(), element => element.Name.LocalName == "table-row-group");
        }

        stream.Position = 0;
        var back = new OdsReader().Read(stream).Require().Sheets[0];
        Assert.Contains(back.RowGroups, group => group is { First: 0, Last: 5 });
        Assert.Contains(back.RowGroups, group => group is { First: 6, Last: 8 });
        Assert.Equal(11d, back.GetValue(new CellAddress(11, 0)).AsNumber());
    }
}
