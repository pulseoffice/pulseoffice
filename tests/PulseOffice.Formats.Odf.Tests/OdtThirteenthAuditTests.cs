using System.IO.Compression;
using System.Xml.Linq;
using PulseOffice.Documents.Blocks;
using PulseOffice.Formats.Odf.Odt;
using Xunit;

namespace PulseOffice.Formats.Odf.Tests;

public sealed class OdtThirteenthAuditTests
{
    [Fact]
    public void Un_corpo_con_un_nome_diverso_da_text_si_legge_come_testo_e_si_dichiara()
    {
        var read = new OdtReader().Read(Odt("<office:texture-mode><text:p>uno</text:p><text:p>due</text:p></office:texture-mode>"));

        Assert.Equal(["uno", "due"], read.Value!.Body.Blocks.OfType<ParagraphBlock>().Select(paragraph => paragraph.Text));
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "ODT180");
    }

    [Fact]
    public void Un_corpo_normale_non_si_dichiara()
    {
        var read = new OdtReader().Read(Odt("<office:text><text:p>uno</text:p></office:text>"));

        Assert.Single(read.Value!.Body.Blocks.OfType<ParagraphBlock>());
        Assert.DoesNotContain(read.Diagnostics, diagnostic => diagnostic.Code == "ODT180");
    }

    [Fact]
    public void Gli_elenchi_dei_pie_di_pagina_conservano_lo_stile_di_elenco()
    {
        const string Spaces = "xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:style=\"urn:oasis:names:tc:opendocument:xmlns:style:1.0\" "
            + "xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:fo=\"urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0\"";
        var styles = $"<office:document-styles {Spaces} office:version=\"1.3\"><office:automatic-styles><style:page-layout style:name=\"Mpm1\"/>"
            + "<text:list-style style:name=\"ML1\"><text:list-level-style-bullet text:level=\"1\" text:bullet-char=\"•\"/></text:list-style></office:automatic-styles>"
            + "<office:master-styles><style:master-page style:name=\"Standard\" style:page-layout-name=\"Mpm1\"><style:footer><text:list text:style-name=\"ML1\">"
            + "<text:list-item><text:p>voce</text:p></text:list-item></text:list></style:footer></style:master-page></office:master-styles></office:document-styles>";
        var read = new OdtReader().Read(Odt("<office:text><text:p>corpo</text:p></office:text>", styles)).Value!;

        var footer = (ParagraphBlock)read.Styles.Page.Find(read.InitialPageStyle)!.Footer.Default[0];
        Assert.Equal("ML1", footer.Content.ListStyleName);
        Assert.NotNull(read.Styles.List.Find("ML1"));
    }

    [Fact]
    public void Una_geometria_a_formule_si_dichiara()
    {
        var read = new OdtReader().Read(Odt("<office:text><text:p>a<draw:custom-shape xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" "
            + "text:anchor-type=\"paragraph\" svg:width=\"2cm\" svg:height=\"2cm\"><draw:enhanced-geometry svg:viewBox=\"0 0 21600 21600\" draw:type=\"mso-spt100\" draw:enhanced-path=\"M 0 ?f1 L ?f2 0 Z N\"/></draw:custom-shape></text:p></office:text>"));

        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "ODT181");
    }

    private static MemoryStream Odt(string body, string? styles = null)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var mimetype = new StreamWriter(archive.CreateEntry("mimetype").Open())) mimetype.Write("application/vnd.oasis.opendocument.text");
            if (styles is not null)
                using (var written = new StreamWriter(archive.CreateEntry("styles.xml").Open())) written.Write(styles);
            using var content = new StreamWriter(archive.CreateEntry("content.xml").Open());
            content.Write("<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" office:version=\"1.3\"><office:body>"
                + body + "</office:body></office:document-content>");
        }

        stream.Position = 0;
        return stream;
    }

    private const string InheritedListHeader = "/Volumes/Dati/develop/libreoffice-ref/sw/qa/extras/ooxmlexport/data/no-numlevel-but-indented.odt";

    [Fact]
    public void Una_voce_senza_numero_di_un_elenco_ereditato_dallo_stile_resta_nell_elenco()
    {
        Assert.SkipWhen(!File.Exists(InheritedListHeader), "Reference corpus not available.");
        PulseOffice.Documents.TextDocument document;
        using (var input = File.OpenRead(InheritedListHeader)) document = new OdtReader().Read(input).Value!;
        using var buffer = new MemoryStream();
        Assert.True(new OdtWriter().Write(document, buffer).Succeeded);
        buffer.Position = 0;
        using var archive = new ZipArchive(buffer, ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
        var content = System.Xml.Linq.XDocument.Parse(reader.ReadToEnd());
        XNamespace text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";

        var header = Assert.Single(content.Descendants(text + "list-header"));
        Assert.Equal(3, header.Ancestors(text + "list").Count() - 1);
        Assert.NotNull(header.Element(text + "h"));
    }
}
