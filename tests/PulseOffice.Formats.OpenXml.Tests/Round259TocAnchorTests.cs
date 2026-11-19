using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Marks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class Round259TocAnchorTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void A_toc_bookmark_targeted_by_a_hyperlink_is_kept()
    {
        using var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            package.AddMainDocumentPart().Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Hyperlink(new W.Run(new W.Text("Intro"))) { Anchor = "_Toc7" }),
                new W.Paragraph(new W.BookmarkStart { Id = "1", Name = "_Toc7" }, new W.Run(new W.Text("Intro")), new W.BookmarkEnd { Id = "1" }),
                new W.Paragraph(new W.BookmarkStart { Id = "2", Name = "_Toc8" }, new W.Run(new W.Text("Other")), new W.BookmarkEnd { Id = "2" })));
        }
        source.Position = 0;

        var document = _composer.Reader(DocxFlavour.Document).Read(source).Value!;

        var names = document.Body.Blocks.OfType<ParagraphBlock>().SelectMany(paragraph => paragraph.Marks).OfType<BookmarkMark>().Select(mark => mark.Name).ToList();
        Assert.Contains("_Toc7", names);
        Assert.DoesNotContain("_Toc8", names);
    }
}
