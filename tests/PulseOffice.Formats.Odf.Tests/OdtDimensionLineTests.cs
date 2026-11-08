using System.IO.Compression;
using PulseOffice.Documents.Objects;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdtDimensionLineTests
{
    [Fact]
    public void La_quota_si_legge_come_quota_e_torna_in_odt()
    {
        var read = new OdtReader().Read(Odt("<text:p>a<draw:measure text:anchor-type=\"paragraph\" svg:x1=\"1.9cm\" svg:y1=\"0.9cm\" svg:x2=\"3.2cm\" svg:y2=\"0.9cm\">"
            + "<text:p><text:measure text:kind=\"value\">13,09</text:measure><text:measure text:kind=\"unit\">mm</text:measure></text:p></draw:measure></text:p>"));

        var placed = Assert.Single(read.Value!.Objects.All);
        var shape = Assert.IsType<ShapeObject>(Assert.IsType<EmbeddedContent>(placed.Content).Value);
        Assert.Equal(ShapeKind.Line, shape.Shape);
        Assert.Equal("mm", shape.Dimension!.Unit);
        Assert.True(shape.Dimension.ShowUnit);
        Assert.Null(shape.Dimension.Label);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODF190");

        using var written = new MemoryStream();
        Assert.True(new OdtWriter().Write(read.Value, written).Succeeded);
        written.Position = 0;
        var again = new OdtReader().Read(written);
        var back = Assert.IsType<ShapeObject>(Assert.IsType<EmbeddedContent>(Assert.Single(again.Value!.Objects.All).Content).Value);
        Assert.Equal(shape.Dimension with { Start = default, End = default }, back.Dimension! with { Start = default, End = default });
        Assert.Equal(shape.Dimension.End.X, back.Dimension.End.X, 2);
        Assert.InRange(Assert.Single(again.Value.Objects.All).Size.Width.Value, placed.Size.Width.Value - 30, placed.Size.Width.Value + 30);
    }

    [Fact]
    public void A_two_line_label_is_written_as_two_paragraphs()
    {
        var read = new OdtReader().Read(Odt("<text:p>a<draw:measure text:anchor-type=\"paragraph\" svg:x1=\"1cm\" svg:y1=\"1cm\" svg:x2=\"4cm\" svg:y2=\"1cm\">"
            + "<text:p>first</text:p><text:p>second</text:p></draw:measure></text:p>"));
        Assert.Equal("first\nsecond", Assert.IsType<ShapeObject>(Assert.IsType<EmbeddedContent>(Assert.Single(read.Value!.Objects.All).Content).Value).Dimension!.Label);

        using var written = new MemoryStream();
        Assert.True(new OdtWriter().Write(read.Value, written).Succeeded);
        written.Position = 0;
        using var archive = new ZipArchive(written);
        var content = System.Xml.Linq.XDocument.Load(archive.GetEntry("content.xml")!.Open());
        var measure = Assert.Single(content.Descendants(Packaging.OdfPackage.Namespaces.Draw + "measure"));
        Assert.Equal(["first", "second"], measure.Elements(Packaging.OdfPackage.Namespaces.Text + "p").Select(paragraph => paragraph.Value));
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
