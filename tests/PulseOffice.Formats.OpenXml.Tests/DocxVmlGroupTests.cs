using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxVmlGroupTests
{
    private const string Corpus = "/Volumes/Dati/develop/libreoffice-ref/sw/qa/extras/ooxmlexport/data/dml-textshape.docx";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument Read(Stream stream)
    {
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    private void AssertGroup(TextDocument document)
    {
        var content = Assert.IsType<EmbeddedContent>(document.Objects.All.Single(item => item.Content is EmbeddedContent { Value: ShapeGroupObject }).Content);
        var group = (ShapeGroupObject)content.Value;
        Assert.Equal(6, group.Children.Count);
        var shapes = group.Children.Select(child => child.Content).OfType<ShapeObject>().ToList();
        Assert.Equal(4, shapes.Count(shape => shape.Shape == ShapeKind.Rectangle));
        Assert.Equal(2, shapes.Count(shape => shape.Shape == ShapeKind.Line));
        Assert.Contains(shapes, shape => shape.Style.Fill.ColorHex.Equals("#FFFF00", StringComparison.OrdinalIgnoreCase) && shape.Style.Line.IsVisible);
        Assert.Equal(["App-V Agent", "Hardware", "MSCAVSS"], content.MemberStories.Values.Select(story => ((ParagraphBlock)story[0]).Text).Order(StringComparer.Ordinal));
        Assert.All(shapes.Where(shape => shape.Shape == ShapeKind.Line), line => Assert.Equal(ArrowEnd.Arrow, line.Style.Line.EndArrow));
    }

    [Fact]
    public void IlGruppoVmlEsternoHaSeiFormeETreCaselle()
    {
        if (!File.Exists(Corpus)) Assert.Skip("Reference corpus not available.");
        using var stream = File.OpenRead(Corpus);
        var result = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DOCX141");
        var document = result.Value!;
        AssertGroup(document);

        using var copy = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, copy).Succeeded);
        copy.Position = 0;
        AssertGroup(Read(copy));
    }
}
