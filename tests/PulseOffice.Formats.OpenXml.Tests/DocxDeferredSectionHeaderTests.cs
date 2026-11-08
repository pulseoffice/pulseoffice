using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxDeferredSectionHeaderTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private MemoryStream Source(bool tableOnly = false, int columns = 1)
    {
        var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var first = main.AddNewPart<HeaderPart>();
            first.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text("First header"))));
            var second = main.AddNewPart<HeaderPart>();
            second.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text("Second header"))));
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("Opening"))),
                new W.Paragraph(new W.ParagraphProperties(Section(main, first, continuous: false)), new W.Run(new W.Text("End of first"))),
                tableOnly
                    ? new W.Table(new W.TableRow(new W.TableCell(new W.Paragraph(new W.Run(new W.Text("Cell"))))))
                    : new W.Paragraph(new W.Run(new W.Text("Continuous start"))),
                Section(main, second, continuous: true, columns)));
        }
        source.Position = 0;
        return source;
    }

    private W.SectionProperties Section(MainDocumentPart main, HeaderPart header, bool continuous, int columns = 1)
    {
        var section = new W.SectionProperties(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(header) });
        if (continuous) section.Append(new W.SectionType { Val = W.SectionMarkValues.Continuous });
        section.Append(
            new W.PageSize { Width = 11906, Height = 16838 },
            new W.PageMargin { Top = 1440, Bottom = 1440, Left = 1440, Right = 1440, Header = 720, Footer = 720 });
        if (columns > 1) section.Append(new W.Columns { ColumnCount = (short)columns, Space = "720" });
        return section;
    }

    [Fact]
    public void AContinuousSectionWithItsOwnHeaderDefersItsPageStyleToTheNextPage()
    {
        using var source = Source();

        var read = _composer.Reader(DocxFlavour.Document).Read(source);

        Assert.True(read.Succeeded);
        Assert.DoesNotContain(read.Diagnostics, loss => loss.Code == "DOCX125");
        var document = read.Value!;
        var lead = document.Body.Blocks.OfType<ParagraphBlock>().Single(block => block.Content.Text == "Continuous start");
        Assert.Null(lead.Content.PageStyleName);
        var deferred = document.Styles.Page.Find(lead.Content.NextPageStyleName!)!;
        Assert.Equal("Second header", ((ParagraphBlock)deferred.Header.Default.Blocks.Single()).Content.Text);
        Assert.Equal("First header", ((ParagraphBlock)document.Styles.Page.Find(document.InitialPageStyle)!.Header.Default.Blocks.Single()).Content.Text);
    }

    [Fact]
    public void ADeferringParagraphThatOpensTheDocumentKeepsItsPageStyleThroughDocx()
    {
        using var source = Source();
        var document = _composer.Reader(DocxFlavour.Document).Read(source).Value!;
        document.Body.RemoveRange(0, 2);
        using var written = new MemoryStream();

        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);

        written.Position = 0;
        var again = _composer.Reader(DocxFlavour.Document).Read(written).Value!;
        Assert.Equal("Second header", ((ParagraphBlock)again.Styles.Page.Find(again.InitialPageStyle)!.Header.Default.Blocks.Single()).Content.Text);
    }

    [Fact]
    public void AStoredPageStyleDoesNotReplaceTheDeferredSectionThatTakesItsNameOnReading()
    {
        using var source = Source();
        var document = _composer.Reader(DocxFlavour.Document).Read(source).Value!;
        var wide = new PageStyle("Wide");
        wide.Header.Enabled = true;
        wide.Header.Default.Add(new ParagraphBlock("h1", "Wide header"));
        document.Styles.Page.Replace(wide);
        var lead = document.Body.Blocks.OfType<ParagraphBlock>().Single(block => block.Content.Text == "Continuous start");
        lead.Content = lead.Content with { NextPageStyleName = "Wide" };
        using var written = new MemoryStream();

        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);

        written.Position = 0;
        var again = _composer.Reader(DocxFlavour.Document).Read(written).Value!;
        var read = again.Body.Blocks.OfType<ParagraphBlock>().Single(block => block.Content.Text == "Continuous start");
        Assert.Equal("Wide header", ((ParagraphBlock)again.Styles.Page.Find(read.Content.NextPageStyleName!)!.Header.Default.Blocks.Single()).Content.Text);
    }

    [Fact]
    public void ADeferredPageStyleIsWrittenAsAContinuousSectionWithItsOwnHeaderAndReadBack()
    {
        using var source = Source();
        var document = _composer.Reader(DocxFlavour.Document).Read(source).Value!;
        using var written = new MemoryStream();

        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);

        written.Position = 0;
        using (var package = WordprocessingDocument.Open(written, false))
        {
            var sections = package.MainDocumentPart!.Document!.Body!.Descendants<W.SectionProperties>().ToList();
            Assert.Equal(2, sections.Count);
            Assert.Equal(W.SectionMarkValues.Continuous, sections[1].GetFirstChild<W.SectionType>()!.Val!.Value);
            var reference = sections[1].Elements<W.HeaderReference>().Single(item => item.Type!.Value == W.HeaderFooterValues.Default);
            var header = (HeaderPart)package.MainDocumentPart.GetPartById(reference.Id!.Value!);
            Assert.Equal("Second header", header.Header!.InnerText);
        }
        written.Position = 0;
        var again = _composer.Reader(DocxFlavour.Document).Read(written);
        Assert.DoesNotContain(again.Diagnostics, loss => loss.Code == "DOCX125");
        var lead = again.Value!.Body.Blocks.OfType<ParagraphBlock>().Single(block => block.Content.Text == "Continuous start");
        Assert.Null(lead.Content.PageStyleName);
        Assert.Equal("Second header", ((ParagraphBlock)again.Value.Styles.Page.Find(lead.Content.NextPageStyleName!)!.Header.Default.Blocks.Single()).Content.Text);
    }

    [Fact]
    public void AContinuousSectionHoldingOnlyATableStillDeclaresTheLoss()
    {
        using var source = Source(tableOnly: true);

        var read = _composer.Reader(DocxFlavour.Document).Read(source);

        Assert.Contains(read.Diagnostics, loss => loss.Code == "DOCX125");
    }

    [Fact]
    public void AContinuousSectionWithColumnsAndItsOwnHeaderKeepsBothThroughDocx()
    {
        using var source = Source(columns: 2);
        var document = _composer.Reader(DocxFlavour.Document).Read(source).Value!;
        Assert.Contains(document.Body.Blocks.OfType<SectionBlock>(), section => section.Columns.Count == 2
            && section.Content.Blocks.FirstOrDefault() is ParagraphBlock { Content.NextPageStyleName: not null });
        using var written = new MemoryStream();

        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);

        written.Position = 0;
        using var package = WordprocessingDocument.Open(written, false);
        var sections = package.MainDocumentPart!.Document!.Body!.Descendants<W.SectionProperties>().ToList();
        var deferred = sections.First(section => section.GetFirstChild<W.Columns>()?.ColumnCount?.Value == 2);
        Assert.Equal(W.SectionMarkValues.Continuous, deferred.GetFirstChild<W.SectionType>()!.Val!.Value);
        var reference = deferred.Elements<W.HeaderReference>().Single(item => item.Type!.Value == W.HeaderFooterValues.Default);
        Assert.Equal("Second header", ((HeaderPart)package.MainDocumentPart.GetPartById(reference.Id!.Value!)).Header!.InnerText);
    }
}
