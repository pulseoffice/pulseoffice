using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Foundation.Pages;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxWordDefaultsTests
{
    private const string Main = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());
    private readonly StyleResolver _resolver = new();

    private TextDocument WordDocument(string body)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Main}><w:body>{body}</w:body></w:document>");
            main.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles($"""
                <w:styles {Main}>
                  <w:docDefaults>
                    <w:rPrDefault><w:rPr><w:rFonts w:asciiTheme="minorHAnsi" w:hAnsiTheme="minorHAnsi"/><w:sz w:val="22"/></w:rPr></w:rPrDefault>
                    <w:pPrDefault><w:pPr><w:spacing w:after="200" w:line="276" w:lineRule="auto"/></w:pPr></w:pPrDefault>
                  </w:docDefaults>
                  <w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style>
                  <w:style w:type="table" w:default="1" w:styleId="TableNormal"><w:name w:val="Normal Table"/>
                    <w:tblPr><w:tblCellMar><w:top w:w="0" w:type="dxa"/><w:left w:w="108" w:type="dxa"/><w:bottom w:w="0" w:type="dxa"/><w:right w:w="108" w:type="dxa"/></w:tblCellMar></w:tblPr>
                  </w:style>
                  <w:style w:type="table" w:styleId="TableGrid"><w:name w:val="Table Grid"/><w:basedOn w:val="TableNormal"/>
                    <w:pPr><w:spacing w:after="0" w:line="240" w:lineRule="auto"/></w:pPr>
                  </w:style>
                </w:styles>
                """);
            main.AddNewPart<ThemePart>().Theme = new DocumentFormat.OpenXml.Drawing.Theme("""
                <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Office">
                  <a:themeElements>
                    <a:clrScheme name="Office"><a:dk1><a:sysClr val="windowText" lastClr="000000"/></a:dk1><a:lt1><a:sysClr val="window" lastClr="FFFFFF"/></a:lt1><a:dk2><a:srgbClr val="1F497D"/></a:dk2><a:lt2><a:srgbClr val="EEECE1"/></a:lt2><a:accent1><a:srgbClr val="4F81BD"/></a:accent1><a:accent2><a:srgbClr val="C0504D"/></a:accent2><a:accent3><a:srgbClr val="9BBB59"/></a:accent3><a:accent4><a:srgbClr val="8064A2"/></a:accent4><a:accent5><a:srgbClr val="4BACC6"/></a:accent5><a:accent6><a:srgbClr val="F79646"/></a:accent6><a:hlink><a:srgbClr val="0000FF"/></a:hlink><a:folHlink><a:srgbClr val="800080"/></a:folHlink></a:clrScheme>
                    <a:fontScheme name="Office"><a:majorFont><a:latin typeface="Cambria"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont><a:minorFont><a:latin typeface="Calibri"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont></a:fontScheme>
                    <a:fmtScheme name="Office"><a:fillStyleLst/><a:lnStyleLst/><a:effectStyleLst/><a:bgFillStyleLst/></a:fmtScheme>
                  </a:themeElements>
                </a:theme>
                """);
        }

        stream.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(stream);
        Assert.True(read.Succeeded);
        return read.Value!;
    }

    private const string Table = "<w:tbl><w:tblPr><w:tblStyle w:val=\"TableGrid\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"4000\"/></w:tblGrid><w:tr><w:tc><w:p><w:r><w:t>cella</w:t></w:r></w:p></w:tc></w:tr></w:tbl>";

    [Fact]
    public void ICaratteriDelTemaDiWordSiRisolvonoNelCarattereDelDocumento()
    {
        var document = WordDocument("<w:p><w:r><w:t>testo</w:t></w:r></w:p>");

        Assert.Equal("Calibri", document.Styles.DefaultCharacter.Font);
    }

    [Fact]
    public void LeSpaziatureDelloStileDiTabellaValgonoPerIParagrafiDelleCelle()
    {
        var document = WordDocument("<w:p><w:r><w:t>prima</w:t></w:r></w:p>" + Table);

        var body = (ParagraphBlock)document.Body.Blocks[0];
        var cell = (ParagraphBlock)document.Body.Blocks.OfType<TableBlock>().Single().Cell(0, 0).Content.Blocks[0];
        Assert.Equal(10, _resolver.EffectiveParagraph(document.Styles, body.Content).SpaceAfter, 3);
        Assert.Equal(0, _resolver.EffectiveParagraph(document.Styles, cell.Content).SpaceAfter, 3);
        Assert.Equal(1.0, _resolver.EffectiveParagraph(document.Styles, cell.Content).LineSpacing, 3);
    }

    [Fact]
    public void IMarginiDelleCelleDiWordNonHannoSpazioSopraESotto()
    {
        var document = WordDocument(Table.Replace("<w:tblStyle w:val=\"TableGrid\"/>", string.Empty, StringComparison.Ordinal));

        var table = document.Body.Blocks.OfType<TableBlock>().Single();
        var look = new TableAppearance(_resolver).Style(document.Styles, table);
        Assert.Equal(5.4, look.CellPadding!.Value, 3);
        Assert.Equal(0, look.VerticalCellPadding!.Value, 3);
    }

    [Fact]
    public void IMarginiPropriDellaTabellaAttraversanoIDueFormati()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var table = new TableBlock("t1", [new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("p1", "x")]))])], [PulseOffice.Foundation.Units.Twip.FromCentimeters(4)])
        {
            Direct = new TableProperties { CellPadding = 4, VerticalCellPadding = 0 },
        };
        document.Body.Add(table);

        foreach (var (writer, reader) in new (ITextDocumentWriter, ITextDocumentReader)[]
                 {
                     (new OdtWriter(), new OdtReader()),
                     (_composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document)),
                 })
        {
            using var stream = new MemoryStream();
            Assert.True(writer.Write(document, stream).Succeeded);
            stream.Position = 0;
            var copy = reader.Read(stream).Value!;
            var look = new TableAppearance(_resolver).Style(copy.Styles, copy.Body.Blocks.OfType<TableBlock>().Single());
            Assert.Equal(4, look.CellPadding!.Value, 2);
            Assert.Equal(0, look.VerticalCellPadding!.Value, 2);
        }
    }

    [Fact]
    public void UnaTabellaCheApreUnaSezioneConservaIlSuoStileDiPaginaNeiDueFormati()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.Page.Add(new PageStyle("Orizzontale") { Orientation = PageOrientation.Landscape, Size = new PulseOffice.Foundation.Units.DocumentSize(PulseOffice.Foundation.Units.Twip.FromCentimeters(29.7), PulseOffice.Foundation.Units.Twip.FromCentimeters(21)) });
        document.Body.Add(new ParagraphBlock("p0", "prima"));
        document.Body.Add(new TableBlock("t1", [new TableRow([new TableCell(new BlockContainer([new ParagraphBlock("p1", "x")]))])], [PulseOffice.Foundation.Units.Twip.FromCentimeters(4)])
        {
            PageStyleName = "Orizzontale",
        });
        document.Body.Add(new ParagraphBlock("p2", "dopo"));

        foreach (var (writer, reader) in new (ITextDocumentWriter, ITextDocumentReader)[]
                 {
                     (new OdtWriter(), new OdtReader()),
                     (_composer.Writer(DocxFlavour.Document), _composer.Reader(DocxFlavour.Document)),
                 })
        {
            using var stream = new MemoryStream();
            Assert.True(writer.Write(document, stream).Succeeded);
            stream.Position = 0;
            var copy = reader.Read(stream).Value!;
            var table = copy.Body.Blocks.OfType<TableBlock>().Single();
            Assert.NotNull(table.PageStyleName);
            Assert.Equal(PageOrientation.Landscape, copy.Styles.Page.Find(table.PageStyleName!)!.Orientation);
        }
    }
}
