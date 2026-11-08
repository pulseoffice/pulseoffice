using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents;
using PulseOffice.Documents.Blocks;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.Odf.Odt;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Text.Rich;
using Xunit;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxTabsRelativeToIndentTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void Le_tabulazioni_relative_al_rientro_diventano_assolute_in_DOCX()
    {
        var document = Indented(relative: true);

        var tabs = WrittenTabs(document);

        Assert.Contains(tabs, tab => tab.Position == "2160" && tab.Value == "left");
    }

    [Fact]
    public void Le_tabulazioni_gia_assolute_si_scrivono_come_sono()
    {
        var document = Indented(relative: false);

        var tabs = WrittenTabs(document);

        Assert.Contains(tabs, tab => tab.Position == "1440" && tab.Value == "left");
    }

    [Fact]
    public void Il_DOCX_si_legge_con_le_tabulazioni_dal_margine_e_l_ODT_lo_conserva()
    {
        var document = Indented(relative: true);
        using var docx = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, docx).Succeeded);
        docx.Position = 0;
        var read = _composer.Reader(DocxFlavour.Document).Read(docx).Value!;

        Assert.False(read.Styles.TabsRelativeToIndent);
        var paragraph = read.Body.Blocks.OfType<ParagraphBlock>().First();
        Assert.Equal(108, Assert.Single(paragraph.Content.TabStops!).Position, 2);

        using var odt = new MemoryStream();
        Assert.True(new OdtWriter().Write(read, odt).Succeeded);
        odt.Position = 0;
        using (var zip = new ZipArchive(odt, ZipArchiveMode.Read, true))
        {
            var settings = XDocument.Load(zip.GetEntry("settings.xml")!.Open());
            Assert.Contains(settings.Descendants(), item => (string?)item.Attribute(XName.Get("name", "urn:oasis:names:tc:opendocument:xmlns:config:1.0")) == "TabsRelativeToIndent" && item.Value == "false");
        }
        odt.Position = 0;
        var again = new OdtReader().Read(odt).Value!;
        Assert.False(again.Styles.TabsRelativeToIndent);

        using var back = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(again, back).Succeeded);
        Assert.Contains(Tabs(back), tab => tab.Position == "2160");
    }

    [Fact]
    public void Senza_impostazione_l_ODT_misura_le_tabulazioni_dal_rientro()
    {
        using var odt = new MemoryStream();
        Assert.True(new OdtWriter().Write(Indented(relative: true), odt).Succeeded);
        odt.Position = 0;

        Assert.True(new OdtReader().Read(odt).Value!.Styles.TabsRelativeToIndent);
    }

    [Fact]
    public void Un_paragrafo_rientrato_che_eredita_le_tabulazioni_dallo_stile_le_riceve_spostate_e_cancella_quelle_dello_stile()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        var standard = document.Styles.Paragraph.Find(DefaultStyleNames.Standard)!;
        standard.Paragraph = standard.Paragraph with { TabStops = [new TabStop(72)] };
        document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("a\tb") { StyleName = DefaultStyleNames.Standard, MarginLeft = 36 })]);

        var tabs = WrittenTabs(document);

        Assert.Contains(tabs, tab => tab.Position == "2160" && tab.Value == "left");
        Assert.Contains(tabs, tab => tab.Position == "1440" && tab.Value == "clear");
    }

    [Fact]
    public void Il_rientro_degli_elenchi_entra_nelle_tabulazioni_scritte_in_DOCX()
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.List.Replace(new ListStyle("Puntato", [new ListLevel(NumberFormat.Bullet) { Indent = 18, Hanging = 18 }]));
        document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("a\tb") { ListStyleName = "Puntato", Bullet = true, TabStops = [new TabStop(72)] })]);

        var tabs = WrittenTabs(document);

        Assert.Contains(tabs, tab => tab.Position == "2160" && tab.Value == "left");
    }

    private TextDocument Indented(bool relative)
    {
        var document = new TextDocument(new DefaultStyleSheetFactory().Create());
        document.Styles.TabsRelativeToIndent = relative;
        document.Body.Replace([new ParagraphBlock("p1", new TextParagraph("a\tb") { MarginLeft = 36, TabStops = [new TabStop(72)] })]);
        return document;
    }

    private IReadOnlyList<(string? Position, string? Value)> WrittenTabs(TextDocument document)
    {
        using var stream = new MemoryStream();
        Assert.True(_composer.Writer(DocxFlavour.Document).Write(document, stream).Succeeded);
        return Tabs(stream);
    }

    private IReadOnlyList<(string? Position, string? Value)> Tabs(MemoryStream stream)
    {
        stream.Position = 0;
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read, true);
        var body = XDocument.Load(zip.GetEntry("word/document.xml")!.Open());
        return [.. body.Descendants(W + "p").Descendants(W + "tab").Where(tab => tab.Parent?.Name == W + "tabs")
            .Select(tab => ((string?)tab.Attribute(W + "pos"), (string?)tab.Attribute(W + "val")))];
    }
}
