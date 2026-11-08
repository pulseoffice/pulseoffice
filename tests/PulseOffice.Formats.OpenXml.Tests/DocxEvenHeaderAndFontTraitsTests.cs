using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Document.Fonts;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxEvenHeaderAndFontTraitsTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private MemoryStream Source(bool evenAndOdd)
    {
        var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var header = main.AddNewPart<HeaderPart>();
            header.Header = new W.Header(new W.Paragraph(new W.Run(new W.Text("Modulo"))));
            var settings = main.AddNewPart<DocumentSettingsPart>();
            settings.Settings = evenAndOdd ? new W.Settings(new W.EvenAndOddHeaders()) : new W.Settings();
            var table = main.AddNewPart<FontTablePart>();
            table.Fonts = new W.Fonts(new W.Font(
                new W.FontFamily { Val = W.FontFamilyValues.Swiss },
                new W.Pitch { Val = W.FontPitchValues.Variable }) { Name = "Myriad Pro" });
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.RunProperties(new W.RunFonts { Ascii = "Myriad Pro", HighAnsi = "Myriad Pro" }), new W.Text("Corpo"))),
                new W.SectionProperties(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = main.GetIdOfPart(header) })));
        }
        source.Position = 0;
        return source;
    }

    [Fact]
    public void Con_pagine_pari_diverse_e_senza_intestazione_pari_le_pagine_pari_restano_senza_intestazione_come_in_Word()
    {
        using var source = Source(evenAndOdd: true);

        var document = _composer.Reader(DocxFlavour.Document).Read(source).Value!;

        var page = document.Styles.Page.Find(document.InitialPageStyle)!;
        Assert.True(page.EvenPagesDifferent);
        Assert.NotNull(page.Header.Even);
        var blank = Assert.Single(page.Header.Even!.Blocks);
        Assert.Equal(string.Empty, Assert.IsType<PulseOffice.Documents.Blocks.ParagraphBlock>(blank).Text);
        Assert.Single(page.Header.Default.Blocks);
    }

    [Fact]
    public void Senza_pagine_pari_diverse_l_intestazione_pari_resta_quella_predefinita()
    {
        using var source = Source(evenAndOdd: false);

        var document = _composer.Reader(DocxFlavour.Document).Read(source).Value!;

        Assert.Null(document.Styles.Page.Find(document.InitialPageStyle)!.Header.Even);
    }

    [Fact]
    public void Famiglia_generica_e_passo_del_carattere_si_leggono_e_si_riscrivono()
    {
        using var source = Source(evenAndOdd: false);
        var document = _composer.Reader(DocxFlavour.Document).Read(source).Value!;
        Assert.Equal(new FontFaceTraits("Myriad Pro", FontGenericFamily.Swiss, FontPitch.Variable), document.Fonts.TraitsOf("Myriad Pro"));

        using var written = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, written).Succeeded);
        written.Position = 0;

        var again = _composer.Reader(DocxFlavour.Document).Read(written).Value!;
        Assert.Equal(new FontFaceTraits("Myriad Pro", FontGenericFamily.Swiss, FontPitch.Variable), again.Fonts.TraitsOf("Myriad Pro"));
    }

    [Fact]
    public void In_ODT_la_famiglia_generica_e_la_pagina_pari_vuota_arrivano_alle_altre_suite()
    {
        using var source = Source(evenAndOdd: true);
        var document = _composer.Reader(DocxFlavour.Document).Read(source).Value!;

        using var odt = new MemoryStream();
        Assert.True(new PulseOffice.Formats.Odf.Odt.OdtWriter().Write(document, odt).Succeeded);
        odt.Position = 0;
        using (var archive = new System.IO.Compression.ZipArchive(odt, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true))
        {
            var styles = System.Xml.Linq.XDocument.Load(archive.GetEntry("styles.xml")!.Open());
            System.Xml.Linq.XNamespace style = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
            System.Xml.Linq.XNamespace text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
            var face = styles.Descendants(style + "font-face").Single(element => (string?)element.Attribute(style + "name") == "Myriad Pro");
            Assert.Equal("swiss", (string?)face.Attribute(style + "font-family-generic"));
            Assert.Equal("variable", (string?)face.Attribute(style + "font-pitch"));
            var left = styles.Descendants(style + "header-left").Single();
            Assert.Single(left.Elements(text + "p"));
            Assert.DoesNotContain(left.Attributes(), attribute => attribute.Name.LocalName == "same-as-default");
        }
        odt.Position = 0;

        var read = new PulseOffice.Formats.Odf.Odt.OdtReader().Read(odt).Value!;
        Assert.Equal(FontGenericFamily.Swiss, read.Fonts.TraitsOf("Myriad Pro")!.Generic);
        Assert.NotNull(read.Styles.Page.Find(read.InitialPageStyle)!.Header.Even);
    }

    [Fact]
    public void Un_simbolo_di_Word_tiene_il_proprio_carattere_e_non_diventa_una_lettera_del_testo()
    {
        var source = new MemoryStream();
        using (var package = WordprocessingDocument.Create(source, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(new W.Paragraph(
                new W.Run(new W.RunProperties(new W.RunFonts { Ascii = "Arial" }), new W.Text("Sì "), new W.SymbolChar { Font = "Wingdings 2", Char = "F0A3" }, new W.Text(" No")))));
        }
        source.Position = 0;

        var document = _composer.Reader(DocxFlavour.Document).Read(source).Value!;

        var runs = document.Body.Blocks.OfType<PulseOffice.Documents.Blocks.ParagraphBlock>().First().Content.Runs;
        var symbol = runs.Single(run => run.Text == "\uF0A3");
        Assert.Equal("Wingdings 2", symbol.Font);
        Assert.All(runs.Where(run => run != symbol), run => Assert.Equal("Arial", run.Font));
    }
}
