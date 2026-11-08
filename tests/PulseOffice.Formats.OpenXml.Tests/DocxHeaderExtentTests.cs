using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxHeaderExtentTests
{
    [Fact]
    public void Il_margine_superiore_in_DOCX_comprende_l_altezza_del_testo_dell_intestazione()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var page = document.Styles.Page.Find(DefaultStyleNames.Standard)!;
        page.Margins = new Margins(Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(2), Twip.FromCentimeters(2));
        page.Header.Enabled = true;
        page.Header.Default.Replace([new ParagraphBlock(document.NextId("p"), "Progetto Aurora", DefaultStyleNames.Header)]);
        document.Body.Add(new ParagraphBlock(document.NextId("p"), "Testo"));
        using var stream = new MemoryStream();

        Assert.True(new DocxComposer(new DirectFormatting(), new DefaultStyleSheetFactory()).Writer(DocxFlavour.Document).Write(document, stream).Succeeded);

        stream.Position = 0;
        using var package = WordprocessingDocument.Open(stream, false);
        var margin = package.MainDocumentPart!.Document!.Body!.Elements<W.SectionProperties>().Single().GetFirstChild<W.PageMargin>()!;
        var bodyStart = Twip.FromCentimeters(2).Value + page.Header.Spacing.Value + 12 * 20;
        Assert.InRange(margin.Top!.Value, bodyStart, bodyStart + 120);
        Assert.Equal((uint)Twip.FromCentimeters(2).Value, margin.Header!.Value);
    }

    [Fact]
    public void Un_intestazione_di_Word_si_legge_senza_spaziatura_aggiunta_e_il_corpo_resta_dove_lo_mette_Word()
    {
        using var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var header = main.AddNewPart<HeaderPart>();
            header.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text("Modulo"))));
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("Corpo"))),
                new W.SectionProperties(
                    new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(header) },
                    new W.PageSize { Width = 11906, Height = 16838 },
                    new W.PageMargin { Top = 360, Bottom = 360, Left = 360, Right = 360, Header = 187, Footer = 187 })));
        }
        source.Position = 0;
        var composer = new DocxComposer(new DirectFormatting(), new DefaultStyleSheetFactory());

        var read = composer.Reader(DocxFlavour.Document).Read(source);

        Assert.True(read.Succeeded);
        var page = read.Value!.Styles.Page.Find(read.Value.InitialPageStyle)!;
        Assert.Equal(Twip.Zero, page.Header.Spacing);
        Assert.Equal(new Twip(173), page.Header.MinimumHeight);
        using var written = new MemoryStream();
        Assert.True(composer.Writer(DocxFlavour.Document).Write(read.Value, written).Succeeded);
        written.Position = 0;
        using var again = WordprocessingDocument.Open(written, false);
        var margin = again.MainDocumentPart!.Document!.Body!.Elements<W.SectionProperties>().Single().GetFirstChild<W.PageMargin>()!;
        Assert.Equal(360, margin.Top!.Value);
    }
}
