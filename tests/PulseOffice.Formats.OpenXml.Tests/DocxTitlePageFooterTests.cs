using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxTitlePageFooterTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private static MemoryStream Package(bool firstFooter)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = package.AddMainDocumentPart();
            var footer = main.AddNewPart<FooterPart>();
            footer.Footer = new W.Footer(new W.Paragraph(new W.Run(new W.Text("Piè normale"))));
            var section = new W.SectionProperties(new W.FooterReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(footer) });
            if (firstFooter)
            {
                var first = main.AddNewPart<FooterPart>();
                first.Footer = new W.Footer(new W.Paragraph(new W.Run(new W.Text("Piè primo"))));
                section.Append(new W.FooterReference { Type = W.HeaderFooterValues.First, Id = main.GetIdOfPart(first) });
            }
            section.Append(new W.TitlePage());
            main.Document = new W.Document(new W.Body(new W.Paragraph(new W.Run(new W.Text("Corpo"))), section));
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void LaPrimaPaginaDiversaSenzaPieProprioRestaSenzaPie()
    {
        var document = _composer.Reader(DocxFlavour.Document).Read(Package(false)).Value!;
        var page = document.Styles.Page.All.Single(style => style.FirstPageDifferent);

        Assert.Equal("Piè normale", Assert.IsType<ParagraphBlock>(page.Footer.Default[0]).Text);
        var first = Assert.IsType<BlockContainer>(page.Footer.First);
        Assert.All(first.Blocks.OfType<ParagraphBlock>(), paragraph => Assert.Equal(string.Empty, paragraph.Text));
    }

    [Fact]
    public void LaPrimaPaginaConPieProprioLoConserva()
    {
        var document = _composer.Reader(DocxFlavour.Document).Read(Package(true)).Value!;
        var page = document.Styles.Page.All.Single(style => style.FirstPageDifferent);

        Assert.Equal("Piè primo", Assert.IsType<ParagraphBlock>(page.Footer.First![0]).Text);
    }
}
