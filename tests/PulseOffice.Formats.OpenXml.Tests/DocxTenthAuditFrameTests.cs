using DocumentFormat.OpenXml.Packaging;
using PulseOffice.Documents;
using PulseOffice.Documents.Objects;
using PulseOffice.Documents.Styles;
using PulseOffice.Formats.OpenXml.Wordprocessing;
using PulseOffice.Foundation.Units;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace PulseOffice.Formats.OpenXml.Tests;

public sealed class DocxTenthAuditFrameTests
{
    private const string Namespace = "xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"";
    private readonly DocxComposer _composer = new(new DirectFormatting(), new DefaultStyleSheetFactory());

    [Fact]
    public void La_cornice_di_uno_stile_di_paragrafo_incornicia_il_paragrafo_e_si_riscrive()
    {
        var document = Read("<w:p><w:r><w:t>prima</w:t></w:r></w:p><w:p><w:pPr><w:pStyle w:val=\"Riquadro\"/><w:framePr w:wrap=\"around\"/></w:pPr><w:r><w:t>nel riquadro</w:t></w:r></w:p>",
            "<w:style w:type=\"paragraph\" w:styleId=\"Base\"><w:name w:val=\"Base\"/><w:pPr><w:framePr w:w=\"4000\" w:hAnchor=\"page\" w:vAnchor=\"text\" w:x=\"1200\" w:y=\"1\"/></w:pPr></w:style>"
            + "<w:style w:type=\"paragraph\" w:styleId=\"Riquadro\"><w:name w:val=\"Riquadro\"/><w:basedOn w:val=\"Base\"/></w:style>");
        var frame = Assert.Single(document.Objects.All);
        Assert.IsType<TextBoxContent>(frame.Content);
        Assert.Equal(new Twip(4000), frame.Size.Width);
        Assert.Equal(new Twip(1200), frame.Placement.X);
        Assert.Equal(HorizontalRelation.Page, frame.Placement.Horizontal);
        Assert.Equal(WrapMode.Parallel, frame.Wrap);

        var written = new MemoryStream();
        _composer.Writer(DocxFlavour.Document).Write(document, written);
        written.Position = 0;
        var again = Assert.Single(_composer.Reader(DocxFlavour.Document).Read(written).Value!.Objects.All);
        Assert.Equal(new Twip(4000), again.Size.Width);
        Assert.IsType<TextBoxContent>(again.Content);
    }

    [Fact]
    public void Uno_stile_senza_cornice_non_incornicia()
    {
        var document = Read("<w:p><w:pPr><w:pStyle w:val=\"Normale\"/></w:pPr><w:r><w:t>testo</w:t></w:r></w:p>",
            "<w:style w:type=\"paragraph\" w:styleId=\"Normale\"><w:name w:val=\"Normale\"/></w:style>");
        Assert.Empty(document.Objects.All);
    }

    [Fact]
    public void Una_tabella_mobile_piu_lunga_di_una_pagina_resta_nel_testo_e_si_dichiara()
    {
        var rows = string.Concat(Enumerable.Range(0, 80).Select(index => $"<w:tr><w:tc><w:p><w:r><w:t>riga {index}</w:t></w:r></w:p></w:tc></w:tr>"));
        var read = _composer.Reader(DocxFlavour.Document).Read(Docx($"<w:tbl><w:tblPr><w:tblpPr w:vertAnchor=\"page\" w:horzAnchor=\"margin\" w:tblpY=\"3000\"/></w:tblPr><w:tblGrid><w:gridCol w:w=\"3000\"/></w:tblGrid>{rows}</w:tbl><w:p/>", null));
        Assert.Empty(read.Value!.Objects.All);
        Assert.Contains(read.Diagnostics, diagnostic => diagnostic.Code == "DOCX172");
    }

    private TextDocument Read(string body, string styles) => _composer.Reader(DocxFlavour.Document).Read(Docx(body, styles)).Value!;

    private static MemoryStream Docx(string body, string? styles)
    {
        var stream = new MemoryStream();
        using (var package = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document($"<w:document {Namespace}><w:body>{body}</w:body></w:document>");
            if (styles is not null) main.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles($"<w:styles {Namespace}>{styles}</w:styles>");
        }

        stream.Position = 0;
        return stream;
    }
}
