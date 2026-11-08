using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxDeletedPictureTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void A_drawing_inside_a_deletion_is_read_into_the_removed_paragraph()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document("<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:v=\"urn:schemas-microsoft-com:vml\"><w:body><w:p><w:del w:id=\"1\" w:author=\"A\"><w:r><w:delText>via</w:delText></w:r><w:r><w:pict><v:rect style=\"width:10pt;height:10pt\"/></w:pict></w:r></w:del></w:p></w:body></w:document>");
        }
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code is "DOCX175" or "DOCX126");
        var deletion = Assert.IsType<PulseOffice.Documents.Annotations.Deletion>(Assert.Single(read.Value!.Changes.All));
        var removed = deletion.Removed.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().Single();
        Assert.Equal("via", removed.Text);
        var mark = Assert.Single(removed.Marks.OfType<PulseOffice.Documents.Marks.ObjectMark>());
        Assert.NotNull(read.Value.Objects.Find(mark.ObjectId));
    }

    [Fact]
    public void A_hyperlink_on_a_group_member_is_read()
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var link = main.AddHyperlinkRelationship(new Uri("https://example.org/"), true).Id;
            var shape = $"<wps:wsp><wps:cNvPr id=\"2\" name=\"R\"><a:hlinkClick r:id=\"{link}\"/></wps:cNvPr><wps:cNvSpPr/><wps:spPr><a:xfrm><a:off x=\"0\" y=\"0\"/><a:ext cx=\"9144\" cy=\"9144\"/></a:xfrm><a:prstGeom prst=\"rect\"/></wps:spPr><wps:bodyPr/></wps:wsp>";
            var group = $"<wpg:wgp><wpg:cNvGrpSpPr/><wpg:grpSpPr/>{shape}{shape.Replace("id=\"2\"", "id=\"3\"")}</wpg:wgp>";
            main.Document = new W.Document($"<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" xmlns:wps=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\" xmlns:wpg=\"http://schemas.microsoft.com/office/word/2010/wordprocessingGroup\"><w:body><w:p><w:r><w:drawing><wp:inline><wp:extent cx=\"9144\" cy=\"9144\"/><wp:docPr id=\"1\" name=\"G\"/><a:graphic><a:graphicData uri=\"http://schemas.microsoft.com/office/word/2010/wordprocessingGroup\">{group}</a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p></w:body></w:document>");
        }
        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        var members = Assert.IsType<PulseOffice.Drawing.Shapes.ShapeGroupObject>(Assert.IsType<PulseOffice.Documents.Objects.EmbeddedContent>(Assert.Single(read.Value!.Objects.All).Content).Value);
        Assert.All(members.Children, child => Assert.Equal("https://example.org/", child.Hyperlink));
    }
}
