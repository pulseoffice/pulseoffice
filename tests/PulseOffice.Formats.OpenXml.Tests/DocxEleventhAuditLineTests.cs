using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;
using A = DocumentFormat.OpenXml.Drawing;
using W = DocumentFormat.OpenXml.Wordprocessing;
using WPS = DocumentFormat.OpenXml.Office2010.Word.DrawingShape;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxEleventhAuditLineTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void La_linea_con_storia_vuota_non_scrive_casella_ne_riempimento()
    {
        var document = new TextDocument();
        var paragraph = new ParagraphBlock("p1", "testo");
        document.Body.Replace([paragraph]);
        var line = new ShapeObject { Id = "line", Shape = ShapeKind.Line };
        document.Objects.Add(new AnchoredObject("o1", new ParagraphAnchor("p1"),
            new EmbeddedContent(line, new BlockContainer([new ParagraphBlock("s1", string.Empty)])),
            new DocumentSize(new Twip(2000), new Twip(0))));

        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        stream.Position = 0;
        using var package = WordprocessingDocument.Open(stream, false);
        var shape = Assert.Single(package.MainDocumentPart!.Document!.Descendants<WPS.WordprocessingShape>());
        Assert.Empty(shape.Descendants<W.TextBoxContent>());
        Assert.NotNull(shape.GetFirstChild<WPS.ShapeProperties>()!.GetFirstChild<A.NoFill>());
    }
}
