using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Drawing.Shapes;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxTextBoxGroupTests
{
    private const string Corpus = "/Volumes/Dati/develop/libreoffice-ref/sw/qa/extras/ooxmlexport/data/groupshape-textbox.docx";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static void AssertGroup(TextDocument document)
    {
        var item = Assert.Single(document.Objects.All);
        var content = Assert.IsType<EmbeddedContent>(item.Content);
        var group = Assert.IsType<ShapeGroupObject>(content.Value);
        Assert.Equal(2, group.Children.Count);
        Assert.All(group.Children.Select(child => Assert.IsType<ShapeObject>(child.Content)), shape =>
        {
            Assert.Equal("#FFFFFF", shape.Style.Fill.ColorHex, StringComparer.OrdinalIgnoreCase);
            Assert.True(shape.Style.Line.IsVisible);
        });
        Assert.Equal(["first", "second"], content.MemberStories.Values.Select(story => ((ParagraphBlock)story[0]).Text).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void UnGruppoDiCaselleDiTestoDiWordRestaUnGruppoConLeSueStorie()
    {
        if (!File.Exists(Corpus)) Assert.Skip("Reference corpus not available.");
        using var stream = File.OpenRead(Corpus);
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        AssertGroup(read.Value!);

        using var docx = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(read.Value!, docx).Succeeded);
        docx.Position = 0;
        AssertGroup(_composer.Reader(DocxFlavour.Document).Read(docx).Value!);

        using var odt = new MemoryStream();
        Assert.True(new OdtWriter().Write(read.Value!, odt).Succeeded);
        odt.Position = 0;
        AssertGroup(new OdtReader().Read(odt).Value!);
    }
}
