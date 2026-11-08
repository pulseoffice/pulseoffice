using System.IO.Compression;
using System.Text;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests.Odt;

public sealed class OdtSenderFieldTests
{
    private const string Content = """
        <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0" xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0" office:version="1.3">
          <office:body><office:text>
            <text:p>Da <text:sender-firstname text:fixed="true">Anna</text:sender-firstname> <text:sender-lastname>Rossi</text:sender-lastname>, <text:sender-city>Pisa</text:sender-city></text:p>
            <text:p>Modello <text:template-name>Lettera</text:template-name></text:p>
          </office:text></office:body>
        </office:document-content>
        """;

    private static MemoryStream Package()
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using (var mime = new StreamWriter(zip.CreateEntry("mimetype", CompressionLevel.NoCompression).Open(), Encoding.ASCII)) mime.Write("application/vnd.oasis.opendocument.text");
            using var content = new StreamWriter(zip.CreateEntry("content.xml").Open(), Encoding.UTF8);
            content.Write(Content);
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void ICampiMittenteSiLeggonoESiRiscrivonoECampiIgnotiSiConservano()
    {
        var read = new OdtReader().Read(Package());
        Assert.True(read.Succeeded);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODF082");
        var document = read.Value!;
        var first = Assert.IsType<ParagraphBlock>(document.Body[0]);
        var senders = first.Content.Runs.Where(run => run.Field == TextFieldKind.Sender).ToList();
        Assert.Equal(["firstname", "lastname", "city"], senders.Select(run => run.FieldArgument));
        Assert.Equal(["Anna", "Rossi", "Pisa"], senders.Select(run => run.Text));
        Assert.True(senders[0].FieldDetail?.Fixed);
        Assert.Equal("Modello Lettera", Assert.IsType<ParagraphBlock>(document.Body[1]).Text);
        Assert.Contains(Assert.IsType<ParagraphBlock>(document.Body[1]).Content.Runs, run => run.Field == TextFieldKind.Preserved && run.Text == "Lettera");

        var again = OdtRoundTripTests.RoundTrip(document);
        var copied = Assert.IsType<ParagraphBlock>(again.Body[0]).Content.Runs.Where(run => run.Field == TextFieldKind.Sender).ToList();
        Assert.Equal(["firstname", "lastname", "city"], copied.Select(run => run.FieldArgument));
        Assert.Equal("Da Anna Rossi, Pisa", again.Body.Blocks.OfType<ParagraphBlock>().First().Text);
        Assert.True(copied[0].FieldDetail?.Fixed);
    }

    [Fact]
    public void UnDocumentoSenzaCampiIgnotiNonDichiaraPerdite()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Replace([new ParagraphBlock("p", "Testo")]);
        using var stream = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, stream).Succeeded);
        stream.Position = 0;
        Assert.DoesNotContain(new OdtReader().Read(stream).Diagnostics, diagnostic => diagnostic.Code == "ODF082");
    }
}
