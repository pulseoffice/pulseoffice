using System.IO.Compression;
using System.Text;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdtRichShapeTextTests
{
    private const string Content = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
        + "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" "
        + "xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" "
        + "xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" office:version=\"1.3\"><office:body><office:text>"
        + "<text:p><draw:custom-shape text:anchor-type=\"paragraph\" draw:name=\"Frame1\" svg:width=\"5cm\" svg:height=\"3cm\" svg:x=\"1cm\" svg:y=\"1cm\">"
        + "<text:p>Figure <text:sequence text:name=\"Figure\" text:formula=\"ooow:Figure+1\" style:num-format=\"1\">1</text:sequence>: didascalia</text:p>"
        + "<draw:enhanced-geometry draw:type=\"{0}\" svg:viewBox=\"0 0 21600 21600\"/></draw:custom-shape></text:p>"
        + "</office:text></office:body></office:document-content>";

    [Theory]
    [InlineData("ooxml-rect", true)]
    [InlineData("rectangle", true)]
    [InlineData("ellipse", false)]
    public void Un_rettangolo_con_campi_nel_testo_si_legge_come_casella_di_testo(string geometry, bool box)
    {
        var read = new OdtReader().Read(Package(string.Format(System.Globalization.CultureInfo.InvariantCulture, Content, geometry))).Value!;
        var item = Assert.Single(read.Objects.All);
        if (!box)
        {
            Assert.IsType<EmbeddedContent>(item.Content);
            return;
        }

        var text = Assert.IsType<TextBoxContent>(item.Content);
        var paragraph = text.Content.Blocks.OfType<ParagraphBlock>().Single();
        Assert.Contains(paragraph.Content.Runs, run => run.Field == PulseOffice.Text.Rich.TextFieldKind.Sequence);
    }

    [Fact]
    public void Un_rettangolo_colorato_con_campi_resta_una_forma_con_il_suo_testo()
    {
        var content = string.Format(System.Globalization.CultureInfo.InvariantCulture, Content, "ooxml-rect")
            .Replace("<office:body>", "<office:automatic-styles><style:style style:name=\"gr1\" style:family=\"graphic\"><style:graphic-properties draw:fill=\"solid\" draw:fill-color=\"#2563eb\"/></style:style></office:automatic-styles><office:body>", StringComparison.Ordinal)
            .Replace("<draw:custom-shape ", "<draw:custom-shape draw:style-name=\"gr1\" ", StringComparison.Ordinal);
        var read = new OdtReader().Read(Package(content)).Value!;

        var embedded = Assert.IsType<EmbeddedContent>(Assert.Single(read.Objects.All).Content);
        Assert.IsType<PulseOffice.Drawing.Shapes.ShapeObject>(embedded.Value);
        Assert.Contains(Assert.IsType<BlockContainer>(embedded.Story).Blocks.OfType<ParagraphBlock>().Single().Content.Runs, run => run.Field == PulseOffice.Text.Rich.TextFieldKind.Sequence);
    }

    private MemoryStream Package(string content)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("mimetype", CompressionLevel.NoCompression).Open(), new UTF8Encoding(false)))
                writer.Write("application/vnd.oasis.opendocument.text");
            using (var writer = new StreamWriter(zip.CreateEntry("content.xml").Open(), new UTF8Encoding(false)))
                writer.Write(content);
        }

        stream.Position = 0;
        return stream;
    }
}
