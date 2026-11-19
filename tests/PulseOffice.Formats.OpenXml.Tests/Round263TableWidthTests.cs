using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round263TableWidthTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static TextDocument Sample(double? percent, bool auto)
    {
        var document = new TextDocumentFactory(new DefaultStyleSheetFactory()).Create();
        var row = new TableRow([new TableCell(new BlockContainer([new ParagraphBlock(document.NextId("p"), "a")]))]);
        document.Body.Insert(0, new TableBlock(document.NextId("t"), [row], [Twip.FromCentimeters(4)]) { WidthPercent = percent, AutoWidth = auto });
        return document;
    }

    private static TableBlock Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        return reader.Read(stream).Value!.Body.Blocks.OfType<TableBlock>().Single();
    }

    [Fact]
    public void A_percent_width_crosses_docx()
    {
        var table = Through(Sample(80, false), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document));

        Assert.Equal(80, table.WidthPercent);
    }

    [Fact]
    public void A_percent_width_crosses_odt()
    {
        var table = Through(Sample(62.5, false), new OdtWriter(), new OdtReader());

        Assert.Equal(62.5, table.WidthPercent);
    }

    [Fact]
    public void An_automatic_width_crosses_both_formats()
    {
        Assert.True(Through(Sample(null, true), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document)).AutoWidth);
        Assert.True(Through(Sample(null, true), new OdtWriter(), new OdtReader()).AutoWidth);
    }
}
