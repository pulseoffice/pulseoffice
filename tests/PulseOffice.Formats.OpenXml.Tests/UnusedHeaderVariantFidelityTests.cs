using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class UnusedHeaderVariantFidelityTests
{
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    private TextDocument Sample(bool firstDifferent)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Body.Add(new ParagraphBlock("p1", "testo"));
        var page = document.Styles.Page.Find(DefaultStyleNames.Standard)!;
        page.Header.Enabled = true;
        page.Header.Default.Replace([new ParagraphBlock("h0", "sempre", DefaultStyleNames.Header)]);
        page.Header.First = new BlockContainer([new ParagraphBlock("h1", "solo prima", DefaultStyleNames.Header)]);
        page.Footer.Enabled = true;
        page.Footer.Default.Replace([new ParagraphBlock("f0", "piede", DefaultStyleNames.Footer)]);
        page.Footer.Even = new BlockContainer([new ParagraphBlock("f2", "solo pari", DefaultStyleNames.Footer)]);
        page.FirstPageDifferent = firstDifferent;
        return document;
    }

    private TextDocument Through(TextDocument document, ITextDocumentWriter writer, ITextDocumentReader reader)
    {
        using var stream = new MemoryStream();
        Assert.True(writer.Write(document, stream).Succeeded);
        stream.Position = 0;
        return reader.Read(stream).Value!;
    }

    private ITextDocumentWriter Docx => _composer.Writer(DocxFlavour.Document);

    private ITextDocumentReader DocxReader => _composer.Reader(DocxFlavour.Document);

    private void AssertHiddenVariantsKept(TextDocument document)
    {
        var page = document.Styles.Page.Find(document.InitialPageStyle)!;
        Assert.False(page.FirstPageDifferent);
        Assert.False(page.EvenPagesDifferent);
        Assert.True(page.Header.Enabled);
        Assert.Equal("sempre", Assert.IsType<ParagraphBlock>(Assert.Single(page.Header.Default.Blocks)).Text);
        Assert.Equal("solo prima", Assert.IsType<ParagraphBlock>(Assert.Single(page.Header.First!.Blocks)).Text);
        Assert.Equal("solo pari", Assert.IsType<ParagraphBlock>(Assert.Single(page.Footer.Even!.Blocks)).Text);
    }

    [Fact]
    public void Le_intestazioni_nascoste_di_prima_pagina_e_pagine_pari_non_sono_piu_una_perdita()
    {
        Assert.DoesNotContain(new OdtWriter().CheckFidelity(Sample(false)).Losses, loss => loss.Code is "ODT007" or "DOCX045");
        Assert.DoesNotContain(Docx.CheckFidelity(Sample(false)).Losses, loss => loss.Code == "DOCX045");
    }

    [Fact]
    public void Andata_e_ritorno_DOCX_conserva_le_intestazioni_spente()
    {
        AssertHiddenVariantsKept(Through(Sample(false), Docx, DocxReader));
    }

    [Fact]
    public void Andata_e_ritorno_ODT_conserva_le_intestazioni_spente()
    {
        AssertHiddenVariantsKept(Through(Sample(false), new OdtWriter(), new OdtReader()));
    }

    [Fact]
    public void Incrocio_DOCX_ODT_DOCX_conserva_le_intestazioni_spente()
    {
        var fromDocx = Through(Sample(false), Docx, DocxReader);
        var fromOdt = Through(fromDocx, new OdtWriter(), new OdtReader());
        AssertHiddenVariantsKept(fromOdt);
        AssertHiddenVariantsKept(Through(fromOdt, Docx, DocxReader));
    }

    [Fact]
    public void Con_prima_pagina_diversa_accesa_l_intestazione_resta_mostrata()
    {
        var page = Through(Through(Sample(true), new OdtWriter(), new OdtReader()), Docx, DocxReader).Styles.Page.Find(DefaultStyleNames.Standard)!;

        Assert.True(page.FirstPageDifferent);
        Assert.Equal("solo prima", Assert.IsType<ParagraphBlock>(Assert.Single(page.Header.First!.Blocks)).Text);
    }

    [Fact]
    public void Una_pagina_pari_nascosta_accanto_a_uno_stile_che_la_mostra_resta_dichiarata_in_DOCX()
    {
        var document = Sample(false);
        var other = new PageStyle("Altra") { EvenPagesDifferent = true };
        document.Styles.Page.Add(other);

        Assert.Contains(Docx.CheckFidelity(document).Losses, loss => loss.Code == "DOCX045");
        Assert.DoesNotContain(new OdtWriter().CheckFidelity(document).Losses, loss => loss.Code == "ODT007");
    }

    [Fact]
    public void In_ODT_le_varianti_nascoste_si_scrivono_con_display_false()
    {
        var odt = Through(Sample(false), new OdtWriter(), new OdtReader());
        using var stream = new MemoryStream();
        new OdtWriter().Write(odt, stream);
        stream.Position = 0;
        using var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
        using var styles = new StreamReader(zip.GetEntry("styles.xml")!.Open());
        var xml = styles.ReadToEnd();

        Assert.Contains("header-first style:display=\"false\"", xml);
        Assert.Contains("footer-left style:display=\"false\"", xml);
    }
}
