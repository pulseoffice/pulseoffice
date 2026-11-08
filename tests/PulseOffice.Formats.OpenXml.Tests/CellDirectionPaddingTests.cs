using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class CellDirectionPaddingTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument Sample()
    {
        var document = new TextDocument();
        TableCell Cell(string text) => new(new BlockContainer([new ParagraphBlock(document.NextId("p"), text, DefaultStyleNames.Standard)]));
        var down = Cell("down");
        down.TextDirection = CellTextDirection.TopToBottom;
        var up = Cell("up");
        up.TextDirection = CellTextDirection.BottomToTop;
        var padded = Cell("padded");
        padded.Padding = new CellPadding(3, 4, 9, 11);
        var table = new TableBlock(document.NextId("t"), [new TableRow([down, up, padded]), new TableRow([Cell("a"), Cell("b"), Cell("c")])],
            [Twip.FromCentimeters(3), Twip.FromCentimeters(3), Twip.FromCentimeters(3)]);
        document.Body.Insert(0, table);
        return document;
    }

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        var read = reader.Read(stream);
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    private void AssertKept(TextDocument document)
    {
        var table = document.Body.Blocks.OfType<TableBlock>().Single();
        Assert.Equal(CellTextDirection.TopToBottom, table.Cell(0, 0).TextDirection);
        Assert.Equal(CellTextDirection.BottomToTop, table.Cell(0, 1).TextDirection);
        Assert.Equal(CellTextDirection.Horizontal, table.Cell(0, 2).TextDirection);
        Assert.Equal(new CellPadding(3, 4, 9, 11), table.Cell(0, 2).Padding);
        Assert.Null(table.Cell(0, 0).Padding);
        Assert.Null(table.Cell(1, 2).Padding);
    }

    [Fact]
    public void DirectionAndPaddingCrossDocx()
    {
        AssertKept(Through(Sample(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document)));
    }

    [Fact]
    public void DirectionAndPaddingCrossOdt()
    {
        AssertKept(Through(Sample(), new OdtWriter(), new OdtReader()));
    }

    [Fact]
    public void DirectionAndPaddingCrossBothFormats()
    {
        var odt = Through(Sample(), new OdtWriter(), new OdtReader());
        AssertKept(Through(odt, _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document)));
        var docx = Through(Sample(), _composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document));
        AssertKept(Through(docx, new OdtWriter(), new OdtReader()));
    }

    [Fact]
    public void DocxWritesTextDirectionAndCellMarginsInSchemaOrder()
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(Sample(), stream).Succeeded);
        stream.Position = 0;
        using var package = WordprocessingDocument.Open(stream, false);
        var cells = package.MainDocumentPart!.Document!.Body!.Descendants<W.TableCell>().ToList();
        Assert.Equal("tbRl", cells[0].TableCellProperties!.TextDirection!.Val!.InnerText);
        Assert.Equal("btLr", cells[1].TableCellProperties!.TextDirection!.Val!.InnerText);
        Assert.Equal("180", cells[2].TableCellProperties!.TableCellMargin!.LeftMargin!.Width!.Value);
        var validator = new DocumentFormat.OpenXml.Validation.OpenXmlValidator();
        Assert.DoesNotContain(validator.Validate(package, TestContext.Current.CancellationToken), error => error.Node is W.TableCellProperties || error.Node?.Parent is W.TableCellProperties);
    }
}
